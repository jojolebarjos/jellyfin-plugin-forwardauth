using System.Security.Cryptography;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ForwardAuth.Api;

[ApiController]
[AllowAnonymous]
[Route("[controller]")]
public class ForwardAuthController(IUserManager userManager, ISessionManager sessionManager, ICryptoProvider cryptoProvider) : ControllerBase
{
    private static readonly string ProviderId = typeof(ForwardAuthProvider).FullName!;

    [HttpPost("Login")]
    public async Task<ActionResult<AuthenticationResult>> Login([FromQuery] string? deviceId)
    {
        var configuration = Plugin.Instance!.Configuration;

        // Get trusted header used for forward authentication
        var username = Request.Headers[configuration.HeaderName].ToString();
        if (string.IsNullOrEmpty(username))
        {
            return StatusCode(StatusCodes.Status401Unauthorized);
        }

        var user = userManager.GetUserByName(username);
        if (user is null)
        {
            // Create user, this is their first login to Jellyfin; saved below along with its password
            user = await userManager.CreateUserAsync(username).ConfigureAwait(false);
            user.AuthenticationProviderId = ProviderId;
        }
        else if (!string.Equals(user.AuthenticationProviderId, ProviderId, StringComparison.Ordinal))
        {
            // This username belongs to another provider
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        // Switching the user to the default provider, which accepts an empty password,
        // should not leave the account open, so give every user an unknown password
        if (string.IsNullOrEmpty(user.Password))
        {
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            user.Password = cryptoProvider.CreatePasswordHash(password).ToString();
            await userManager.UpdateUserAsync(user).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(configuration.AdminGroup))
        {
            // Authentik separates groups with a pipe
            var groups = Request.Headers[configuration.GroupsHeaderName].ToString().Split('|');
            var isAdmin = groups.Contains(configuration.AdminGroup, StringComparer.Ordinal);

            // Go through the policy, as the dashboard does, since SetPermission with UpdateUserAsync was reported not to persist
            var policy = userManager.GetUserDto(user).Policy;
            if (policy.IsAdministrator != isAdmin)
            {
                policy.IsAdministrator = isAdmin;
                await userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
            }
        }

        var request = new AuthenticationRequest
        {
            UserId = user.Id,
            Username = username,
            App = "Jellyfin Web",
            AppVersion = "10.11.0",
            DeviceId = string.IsNullOrEmpty(deviceId) ? "forward-auth" : deviceId,
            DeviceName = "Browser",
            RemoteEndPoint = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
        };
        try
        {
            return await sessionManager.AuthenticateDirect(request).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status401Unauthorized);
        }
    }

    [HttpGet("inject.js")]
    public ActionResult InjectScript()
    {
        using var stream = GetType().Assembly.GetManifestResourceStream("Jellyfin.Plugin.ForwardAuth.Pages.inject.js")!;
        using var reader = new StreamReader(stream);

        // Do not cache, as this would get stale in case of update
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Content(reader.ReadToEnd(), "text/javascript");
    }
}
