using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ForwardAuth.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        HeaderName = "X-authentik-username";
    }

    public string HeaderName { get; set; }
}
