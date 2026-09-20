using System.Text.Json;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ForwardAuth.Api;

[ApiController]
[AllowAnonymous]
[Route("[controller]")]
public class ForwardAuthController(IUserManager userManager, ISessionManager sessionManager) : ControllerBase
{
    private sealed class NoPasswordProvider;

    // Use fake unregistered provider, to prevent any password-based login
    private static readonly string ProviderId = typeof(NoPasswordProvider).FullName!;

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
            // Create user, this is their first login to Jellyfin
            user = await userManager.CreateUserAsync(username).ConfigureAwait(false);
            user.AuthenticationProviderId = ProviderId;
            await userManager.UpdateUserAsync(user).ConfigureAwait(false);
        }
        else if (!string.Equals(user.AuthenticationProviderId, ProviderId, StringComparison.Ordinal))
        {
            // This username belongs to an account we do not manage
            return StatusCode(StatusCodes.Status403Forbidden);
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
        var configuration = Plugin.Instance!.Configuration;
        using var stream = GetType().Assembly.GetManifestResourceStream("Jellyfin.Plugin.ForwardAuth.Pages.inject.js")!;
        using var reader = new StreamReader(stream);
        var script = reader.ReadToEnd().Replace(
            "__LOGOUT_REDIRECT_URL__",
            JsonSerializer.Serialize(configuration.LogoutRedirectUrl),
            StringComparison.Ordinal);

        // Do not cache, as this would get stale in case of settings change
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Content(script, "text/javascript");
    }
}
