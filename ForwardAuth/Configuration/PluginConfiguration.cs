using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ForwardAuth.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        HeaderName = "X-authentik-username";
        LogoutRedirectUrl = "/outpost.goauthentik.io/sign_out";
    }

    public string HeaderName { get; set; }

    public string LogoutRedirectUrl { get; set; }
}
