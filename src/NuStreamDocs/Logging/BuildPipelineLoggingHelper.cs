// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Logging;

/// <summary>
/// Source-generated <see cref="ILogger"/> messages for the
/// <see cref="NuStreamDocs.Building.BuildPipeline"/> driver.
/// </summary>
internal static partial class BuildPipelineLoggingHelper
{
    /// <summary>Logs the build start and configuration summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="inputRoot">Absolute input docs root.</param>
    /// <param name="outputRoot">Absolute output site root.</param>
    /// <param name="pluginCount">Number of registered plugins.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Build starting: input={InputRoot} output={OutputRoot} plugins={PluginCount}")]
    public static partial void LogBuildStart(ILogger logger, string inputRoot, string outputRoot, int pluginCount);

    /// <summary>Logs the build end-of-run summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pageCount">Total pages processed.</param>
    /// <param name="cacheHits">Pages reused from the previous-build manifest.</param>
    /// <param name="elapsedMs">Wall-clock duration in milliseconds.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Build complete: {PageCount} page(s) processed, {CacheHits} cache hit(s), elapsed={ElapsedMs}ms")]
    public static partial void LogBuildComplete(ILogger logger, int pageCount, int cacheHits, long elapsedMs);

    /// <summary>Logs entry into the configure phase before any plugin's <c>OnConfigureAsync</c> fires.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginCount">Number of plugins to configure.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring {PluginCount} plugin(s)...")]
    public static partial void LogConfigureStart(ILogger logger, int pluginCount);

    /// <summary>Logs the start of one plugin's <c>OnConfigureAsync</c> hook (Debug — most plugins are no-ops on this hook so the per-plugin trail is too noisy at Info level).</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name (<see cref="NuStreamDocs.Plugins.IDocPlugin.Name"/>).</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Configuring plugin: {PluginName}")]
    public static partial void LogPluginConfigure(ILogger logger, string pluginName);

    /// <summary>Logs entry into the parallel render phase.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="parallelism">Effective <see cref="System.Threading.Tasks.ParallelOptions.MaxDegreeOfParallelism"/>.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Rendering pages (parallelism={Parallelism})...")]
    public static partial void LogRenderStart(ILogger logger, int parallelism);

    /// <summary>Logs the end of the parallel render phase.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pageCount">Pages processed in this phase.</param>
    /// <param name="elapsedMs">Phase duration in milliseconds.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Render complete: {PageCount} page(s) in {ElapsedMs}ms")]
    public static partial void LogRenderComplete(ILogger logger, int pageCount, long elapsedMs);

    /// <summary>Logs entry into the finalize phase before any plugin's <c>OnFinalizeAsync</c> fires.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginCount">Number of plugins to finalize.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Finalizing {PluginCount} plugin(s)...")]
    public static partial void LogFinalizeStart(ILogger logger, int pluginCount);

    /// <summary>Logs the start of one plugin's <c>OnFinalizeAsync</c> hook (Debug — most plugins are no-ops on this hook).</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Finalizing plugin: {PluginName}")]
    public static partial void LogPluginFinalize(ILogger logger, string pluginName);

    /// <summary>Logs a per-page completion at debug level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="relativePath">Page relative path.</param>
    /// <param name="cacheHit">True when the page was reused from the manifest.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page processed: {RelativePath} (cacheHit={CacheHit})")]
    public static partial void LogPageProcessed(ILogger logger, string relativePath, bool cacheHit);
}
