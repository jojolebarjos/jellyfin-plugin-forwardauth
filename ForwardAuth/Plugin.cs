using System;
using Jellyfin.Plugin.ForwardAuth.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ForwardAuth;

public class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    public override string Name => "Forward Auth";

    public override Guid Id => Guid.Parse("29445226-96ee-4bf7-b9e7-8fbee1aa7ab1");
}
