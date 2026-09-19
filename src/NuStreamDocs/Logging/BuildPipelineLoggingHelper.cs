// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Logging;

/// <summary>Source-generated <see cref="ILogger"/> messages for the <see cref="Building.BuildPipeline"/> driver.</summary>
internal static partial class BuildPipelineLoggingHelper
{
    /// <summary>Warns that two generated pages target the same output.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="generatedPath">Skipped generated page.</param>
    /// <param name="existingPath">Page whose contents are preserved.</param>
    /// <param name="outputPath">Conflicting output destination.</param>
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Generated page '{GeneratedPath}' is ignored: generated page '{ExistingPath}' already owns output '{OutputPath}'. Resolve the duplicate generated page registrations.")]
    internal static partial void LogGeneratedPageConflict(ILogger logger, FilePath generatedPath, FilePath existingPath, FilePath outputPath);

    /// <summary>Warns that generated content takes precedence over an existing source page.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="sourcePath">Ignored source page.</param>
    /// <param name="generatedPath">Generated page whose content is used.</param>
    /// <param name="outputPath">Selected output destination.</param>
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Source page '{SourcePath}' is ignored for this build because generated page '{GeneratedPath}' owns output '{OutputPath}'. The source file has not been changed.")]
    internal static partial void LogSourcePageIgnoredForGeneratedPage(ILogger logger, FilePath sourcePath, FilePath generatedPath, FilePath outputPath);

    /// <summary>Warns that a source page conflicts with the first page discovered for an output.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="skippedPath">Skipped source page.</param>
    /// <param name="existingPath">Page whose contents are preserved.</param>
    /// <param name="outputPath">Conflicting output destination.</param>
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Source page '{SkippedPath}' is ignored: page '{ExistingPath}' already owns output '{OutputPath}'. Rename or exclude one of these pages to give each a distinct output.")]
    internal static partial void LogSourcePageConflict(ILogger logger, FilePath skippedPath, FilePath existingPath, FilePath outputPath);

    /// <summary>Logs the build start and configuration summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="inputRoot">Absolute input docs root.</param>
    /// <param name="outputRoot">Absolute output site root.</param>
    /// <param name="pluginCount">Number of registered plugins.</param>
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Build starting: input={InputRoot} output={OutputRoot} plugins={PluginCount}")]
    internal static partial void LogBuildStart(ILogger logger, string inputRoot, string outputRoot, int pluginCount);

    /// <summary>Logs the build end-of-run summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pageCount">Total pages processed.</param>
    /// <param name="cacheHits">Pages reused from the previous-build manifest.</param>
    /// <param name="elapsedSeconds">Wall-clock duration in seconds (three decimal places).</param>
    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Build complete: {PageCount} page(s) processed, {CacheHits} cache hit(s), elapsed={ElapsedSeconds:F3}s")]
    internal static partial void LogBuildComplete(ILogger logger, int pageCount, int cacheHits, double elapsedSeconds);

    /// <summary>Logs entry into the configure phase before any plugin's <c>OnConfigureAsync</c> fires.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginCount">Number of plugins to configure.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring {PluginCount} plugin(s)...")]
    internal static partial void LogConfigureStart(ILogger logger, int pluginCount);

    /// <summary>Logs the start of one plugin's <c>OnConfigureAsync</c> hook (Debug — most plugins are no-ops on this hook so the per-plugin trail is too noisy at Info level).</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name (<see cref="NuStreamDocs.Plugins.IPlugin.Name"/>).</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Configuring plugin: {PluginName}")]
    internal static partial void LogPluginConfigure(ILogger logger, string pluginName);

    /// <summary>UTF-8 byte overload for <see cref="LogPluginConfigure(ILogger, string)"/>; the encode step only runs when Debug is enabled.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name as UTF-8 bytes.</param>
    internal static void LogPluginConfigure(ILogger logger, ReadOnlySpan<byte> pluginName)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        LogPluginConfigure(logger, Encoding.UTF8.GetString(pluginName));
    }

    /// <summary>Logs entry into the parallel render phase.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="parallelism">Effective <see cref="System.Threading.Tasks.ParallelOptions.MaxDegreeOfParallelism"/>.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Rendering pages (parallelism={Parallelism})...")]
    internal static partial void LogRenderStart(ILogger logger, int parallelism);

    /// <summary>Logs the end of the parallel render phase.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pageCount">Pages processed in this phase.</param>
    /// <param name="elapsedSeconds">Phase duration in seconds (three decimal places).</param>
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Render complete: {PageCount} page(s) in {ElapsedSeconds:F3}s")]
    internal static partial void LogRenderComplete(ILogger logger, int pageCount, double elapsedSeconds);

    /// <summary>Logs the docs static-asset copy step result.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="assetCount">Number of files copied from the input docs tree to the output site tree.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Copied {AssetCount} static asset(s) from docs/ to site/")]
    internal static partial void LogAssetsCopied(ILogger logger, int assetCount);

    /// <summary>Logs entry into the finalize phase before any plugin's <c>OnFinalizeAsync</c> fires.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginCount">Number of plugins to finalize.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Finalizing {PluginCount} plugin(s)...")]
    internal static partial void LogFinalizeStart(ILogger logger, int pluginCount);

    /// <summary>Logs the start of one plugin's <c>OnFinalizeAsync</c> hook (Debug — most plugins are no-ops on this hook).</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Finalizing plugin: {PluginName}")]
    internal static partial void LogPluginFinalize(ILogger logger, string pluginName);

    /// <summary>UTF-8 byte overload for <see cref="LogPluginFinalize(ILogger, string)"/>; the encode step only runs when Debug is enabled.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name as UTF-8 bytes.</param>
    internal static void LogPluginFinalize(ILogger logger, ReadOnlySpan<byte> pluginName)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        LogPluginFinalize(logger, Encoding.UTF8.GetString(pluginName));
    }

    /// <summary>Logs a per-page completion at debug level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="relativePath">Page relative path.</param>
    /// <param name="cacheHit">True when the page was reused from the manifest.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page processed: {RelativePath} (cacheHit={CacheHit})")]
    internal static partial void LogPageProcessed(ILogger logger, string relativePath, bool cacheHit);
}
