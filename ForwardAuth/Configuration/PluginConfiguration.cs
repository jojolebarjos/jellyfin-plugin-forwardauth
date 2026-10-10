using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ForwardAuth.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        HeaderName = "X-authentik-username";
        GroupsHeaderName = "X-authentik-groups";
        AdminGroup = string.Empty;
    }

    public string HeaderName { get; set; }

    public string GroupsHeaderName { get; set; }

    public string AdminGroup { get; set; }
}
