// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Logging;

/// <summary>Source-generated <see cref="ILogger"/> messages for the per-plugin timing summary.</summary>
internal static partial class PluginTimingLoggingHelper
{
    /// <summary>Logs the summary header.</summary>
    /// <param name="logger">Target logger.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin timing summary (sorted by total time):")]
    public static partial void LogPluginTimingHeader(ILogger logger);

    /// <summary>Logs one row of the per-plugin total time at Info level — fired for plugins whose total cleared the significance threshold.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="elapsedSeconds">Total time in seconds (millisecond resolution).</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "  {PluginName,-40} {ElapsedSeconds:F3}s")]
    public static partial void LogPluginTimingRow(ILogger logger, string pluginName, double elapsedSeconds);

    /// <summary>Logs one row of the per-plugin total time at Debug level — fired for plugins whose total fell under the significance threshold.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="elapsedSeconds">Total time in seconds (millisecond resolution).</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "  {PluginName,-40} {ElapsedSeconds:F3}s")]
    public static partial void LogPluginTimingDebug(ILogger logger, string pluginName, double elapsedSeconds);
}
