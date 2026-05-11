// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Pagefind.Logging;

/// <summary>Source-generated logging helpers for <see cref="PagefindIgnoreInjector"/>.</summary>
internal static partial class PagefindIgnoreInjectorLogging
{
    /// <summary>Logs the count of files marked with <c>data-pagefind-ignore</c>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="count">Number of files modified.</param>
    public static void LogInjected(ILogger logger, int count)
    {
        if (count == 0 || !logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        LogInjectedCore(logger, count);
    }

    /// <summary>Source-generated emitter for <see cref="LogInjected"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="count">File count.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Marked {Count} page(s) with data-pagefind-ignore so Pagefind will skip them.")]
    private static partial void LogInjectedCore(ILogger logger, int count);
}
