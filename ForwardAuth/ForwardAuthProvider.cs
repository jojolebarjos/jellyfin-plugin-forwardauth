using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Authentication;

namespace Jellyfin.Plugin.ForwardAuth;

public class ForwardAuthProvider : IAuthenticationProvider
{
    public string Name => "Forward Auth";

    public bool IsEnabled => true;

    public Task<ProviderAuthenticationResult> Authenticate(string username, string password)
    {
        throw new AuthenticationException("Users managed by forward auth cannot log in with a password.");
    }

    public Task ChangePassword(User user, string newPassword)
    {
        throw new NotSupportedException("Users managed by forward auth have no password.");
    }
}
