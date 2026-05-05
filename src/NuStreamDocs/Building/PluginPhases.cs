// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>
/// Per-phase plugin arrays sorted by priority bid.
/// </summary>
/// <remarks>
/// Computed once at build start by <see cref="Partition"/>. The build
/// pipeline iterates only the participants for each phase; plugins
/// missing from a phase are not in the corresponding array.
/// </remarks>
internal sealed class PluginPhases
{
    /// <summary>Gets the sorted configure-phase participants.</summary>
    public IBuildConfigurePlugin[] Configures { get; init; } = [];

    /// <summary>Gets the sorted discover-phase participants.</summary>
    public IBuildDiscoverPlugin[] Discovers { get; init; } = [];

    /// <summary>Gets the sorted pre-render-phase participants.</summary>
    public IPagePreRenderPlugin[] PreRenders { get; init; } = [];

    /// <summary>Gets the sorted post-render-phase participants.</summary>
    public IPagePostRenderPlugin[] PostRenders { get; init; } = [];

    /// <summary>Gets the sorted scan-phase participants.</summary>
    public IPageScanPlugin[] Scans { get; init; } = [];

    /// <summary>Gets the sorted resolve-phase participants.</summary>
    public IBuildResolvePlugin[] Resolves { get; init; } = [];

    /// <summary>Gets the sorted post-resolve-phase participants.</summary>
    public IPagePostResolvePlugin[] PostResolves { get; init; } = [];

    /// <summary>Gets the sorted finalize-phase participants.</summary>
    public IBuildFinalizePlugin[] Finalizes { get; init; } = [];

    /// <summary>Gets a value indicating whether the build needs to buffer rendered pages until the cross-page barrier completes.</summary>
    public bool NeedsCrossPageBarrier => Resolves.Length > 0 || PostResolves.Length > 0;

    /// <summary>Partitions a flat plugin list into per-phase sorted arrays.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>The per-phase plugin arrays.</returns>
    public static PluginPhases Partition(IPlugin[] plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        return new()
        {
            Configures = Collect<IBuildConfigurePlugin>(plugins, static p => p.ConfigurePriority),
            Discovers = Collect<IBuildDiscoverPlugin>(plugins, static p => p.DiscoverPriority),
            PreRenders = Collect<IPagePreRenderPlugin>(plugins, static p => p.PreRenderPriority),
            PostRenders = Collect<IPagePostRenderPlugin>(plugins, static p => p.PostRenderPriority),
            Scans = Collect<IPageScanPlugin>(plugins, static p => p.ScanPriority),
            Resolves = Collect<IBuildResolvePlugin>(plugins, static p => p.ResolvePriority),
            PostResolves = Collect<IPagePostResolvePlugin>(plugins, static p => p.PostResolvePriority),
            Finalizes = Collect<IBuildFinalizePlugin>(plugins, static p => p.FinalizePriority)
        };
    }

    /// <summary>Selects every <typeparamref name="T"/>-implementing plugin and sorts by the supplied priority projection.</summary>
    /// <typeparam name="T">Phase-specific interface.</typeparam>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="getPriority">Projects a plugin to its phase-specific priority bid.</param>
    /// <returns>Sorted phase-participant array.</returns>
    private static T[] Collect<T>(IPlugin[] plugins, Func<T, PluginPriority> getPriority)
        where T : class, IPlugin
    {
        List<T> matches = new(plugins.Length);
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is T t)
            {
                matches.Add(t);
            }
        }

        matches.Sort((a, b) => getPriority(a).CompareTo(getPriority(b)));
        return [.. matches];
    }
}
