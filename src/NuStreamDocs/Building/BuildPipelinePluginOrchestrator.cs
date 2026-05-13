// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>
/// Static helper for orchestrating build pipeline plugin phases.
/// </summary>
internal static class BuildPipelinePluginOrchestrator
{
    /// <summary>Fires <see cref="IBuildConfigurePlugin.ConfigureAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="configures">Sorted configure participants.</param>
    /// <param name="allPlugins">Every registered plugin (passed to participants for sibling discovery).</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="crossPageMarkers">Cross-page marker registry plugins seed during configure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's configure hook has settled.</returns>
    public static async Task FireConfigureAsync(
        IBuildConfigurePlugin[] configures,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        CrossPageMarkerRegistry crossPageMarkers,
        CancellationToken cancellationToken)
    {
        if (configures.Length is 0)
        {
            return;
        }

        var options = shell.Options;
        BuildConfigureContext context = new(shell.InputRoot, shell.OutputRoot, allPlugins, crossPageMarkers)
        {
            UseDirectoryUrls = options.UseDirectoryUrls,
            SiteName = options.SiteName ?? [],
            SiteUrl = options.SiteUrl ?? [],
            SiteAuthor = options.SiteAuthor ?? []
        };
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < configures.Length; i++)
        {
            var plugin = configures[i];
            BuildPipelineLoggingHelper.LogPluginConfigure(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.ConfigureAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Fires <see cref="IBuildDiscoverPlugin.DiscoverAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="discovers">Sorted discover participants.</param>
    /// <param name="allPlugins">Every registered plugin.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="syntheticPages">
    /// Sink that collects in-memory pages plugins want to flow through the regular render
    /// pipeline without writing intermediate <c>.md</c> files into the source folder.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's discover hook has settled.</returns>
    public static async Task FireDiscoverAsync(
        IBuildDiscoverPlugin[] discovers,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        SyntheticPageSink syntheticPages,
        CancellationToken cancellationToken)
    {
        if (discovers.Length is 0)
        {
            return;
        }

        BuildDiscoverContext context = new(shell.InputRoot, shell.OutputRoot, allPlugins, syntheticPages)
        {
            UseDirectoryUrls = shell.Options.UseDirectoryUrls
        };
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < discovers.Length; i++)
        {
            var plugin = discovers[i];
            BuildPipelineLoggingHelper.LogPluginConfigure(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.DiscoverAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Fires <see cref="IBuildResolvePlugin.ResolveAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="resolves">Sorted resolve participants.</param>
    /// <param name="allPlugins">Every registered plugin.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's resolve hook has settled.</returns>
    public static async Task FireResolveAsync(
        IBuildResolvePlugin[] resolves,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        CancellationToken cancellationToken)
    {
        if (resolves.Length is 0)
        {
            return;
        }

        BuildResolveContext context = new(shell.OutputRoot, allPlugins);
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < resolves.Length; i++)
        {
            var plugin = resolves[i];
            BuildPipelineLoggingHelper.LogPluginFinalize(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Fires <see cref="IBuildFinalizePlugin.FinalizeAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="finalizes">Sorted finalize participants.</param>
    /// <param name="allPlugins">Every registered plugin.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's finalize hook has settled.</returns>
    public static async Task FireFinalizeAsync(
        IBuildFinalizePlugin[] finalizes,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        CancellationToken cancellationToken)
    {
        if (finalizes.Length is 0)
        {
            return;
        }

        BuildFinalizeContext context = new(shell.OutputRoot, allPlugins);
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < finalizes.Length; i++)
        {
            var plugin = finalizes[i];
            BuildPipelineLoggingHelper.LogPluginFinalize(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.FinalizeAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
