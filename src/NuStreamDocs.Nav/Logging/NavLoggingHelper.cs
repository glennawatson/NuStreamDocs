// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav.Logging;

/// <summary>
/// Source-generated logging entry points for <see cref="NavPlugin"/> and
/// <see cref="NavTreeBuilder"/>.
/// </summary>
/// <remarks>
/// All methods are <see cref="LoggerMessageAttribute"/> partials so the
/// generator emits the underlying <c>EventId</c> + cached delegate; we
/// never call <see cref="ILogger"/> extension methods directly. Debug-
/// level entries that bind expensive arguments should be invoked via
/// <c>NuStreamDocs.Logging.LogInvokerHelper.Invoke</c> at the call site
/// so the projection only runs when the level is enabled.
/// </remarks>
internal static partial class NavLoggingHelper
{
    /// <summary>Logs the start of a nav build.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="inputRoot">Absolute docs input root.</param>
    /// <param name="pageCount">Number of source pages discovered before the nav prunes them.</param>
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Nav build starting under {InputRoot} ({PageCount} candidate page(s))")]
    public static partial void LogNavBuildStart(ILogger logger, string inputRoot, int pageCount);

    /// <summary>Logs nav build completion.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="sectionCount">Total section nodes in the rendered tree.</param>
    /// <param name="leafCount">Total leaf pages in the rendered tree.</param>
    /// <param name="prunedCount">Number of files filtered out by includes/excludes / hidden sections.</param>
    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "Nav build complete: {SectionCount} section(s), {LeafCount} leaf page(s), {PrunedCount} pruned")]
    public static partial void LogNavBuildComplete(ILogger logger, int sectionCount, int leafCount, int prunedCount);

    /// <summary>Logs a single pruning decision.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="relativePath">Source-relative path that was pruned.</param>
    /// <param name="reason">Free-form reason (e.g. <c>"glob excluded"</c>, <c>"empty section"</c>).</param>
    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Debug,
        Message = "Nav pruned {RelativePath}: {Reason}")]
    public static partial void LogNavPruned(ILogger logger, string relativePath, string reason);

    /// <summary>Logs the count of pages on disk that aren't reachable through the nav tree.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="orphanCount">Number of orphan pages.</param>
    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Warning,
        Message = "Nav orphan check: {OrphanCount} page(s) exist in the input directory but are not included in the nav configuration")]
    public static partial void LogOrphanPagesHeader(ILogger logger, int orphanCount);

    /// <summary>Logs one orphan page; one entry per file so log filters can pick out specific paths.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="relativePath">Source-relative path of the orphan page.</param>
    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Warning,
        Message = "Nav orphan: {RelativePath}")]
    public static partial void LogOrphanPage(ILogger logger, string relativePath);
}
