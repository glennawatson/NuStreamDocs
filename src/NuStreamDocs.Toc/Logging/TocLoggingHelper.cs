// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc.Logging;

/// <summary>
/// Source-generated logging entry points for <see cref="TocPlugin"/>.
/// </summary>
/// <remarks>
/// All methods are <see cref="LoggerMessageAttribute"/> partials so the
/// generator emits the underlying <c>EventId</c> + cached delegate; we
/// never call <see cref="ILogger"/> extension methods directly.
/// </remarks>
internal static partial class TocLoggingHelper
{
    /// <summary>Logs the start of TOC processing for a page.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="relativePath">Source-relative path of the page being processed.</param>
    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Debug,
        Message = "Toc start for {RelativePath}")]
    public static partial void LogTocStart(ILogger logger, string relativePath);

    /// <summary>Logs the completion of TOC processing for a page.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="relativePath">Source-relative path of the page processed.</param>
    /// <param name="headingCount">Number of headings found.</param>
    /// <param name="slugCollisions">Number of slug collisions resolved with a numeric suffix.</param>
    /// <param name="elapsedMs">Elapsed milliseconds spent in the plugin's render pass.</param>
    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Debug,
        Message = "Toc complete for {RelativePath}: {HeadingCount} heading(s), {SlugCollisions} collision(s), {ElapsedMs} ms")]
    public static partial void LogTocComplete(ILogger logger, string relativePath, int headingCount, int slugCollisions, long elapsedMs);
}
