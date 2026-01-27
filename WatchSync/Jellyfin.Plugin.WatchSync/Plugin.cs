using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WatchSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.WatchSync;

/// <summary>
/// Main plugin class for cross-library watch state synchronization.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Unique plugin identifier.
    /// </summary>
    public static readonly Guid PluginId = new("aa4fe598-07fd-41b5-bf1d-65abab9980db");

    /// <summary>
    /// Singleton instance of the plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Initializes a new instance of the plugin.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "WatchSync";

    /// <inheritdoc />
    public override string Description => "Automatically synchronizes watch history between libraries of different qualities (4K/HD).";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <summary>
    /// Returns the plugin's web pages.
    /// </summary>
    /// <returns>Collection of web pages.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        };
    }
}
