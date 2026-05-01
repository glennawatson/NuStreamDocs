// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog.Common.Logging;

/// <summary>
/// Source-generated logging helpers for the blog plugins.
/// </summary>
internal static partial class BlogLoggingHelper
{
    /// <summary>Logs the start of post discovery.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="inputRoot">Posts directory being scanned.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovering blog posts under {InputRoot}")]
    public static partial void LogDiscoveryStart(ILogger logger, string inputRoot);

    /// <summary>Logs the end of post discovery.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="postCount">Posts accepted.</param>
    /// <param name="draftsSkipped">Posts skipped because they were drafts or unparseable.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Blog post discovery complete: {PostCount} post(s), {DraftsSkipped} draft(s) skipped")]
    public static partial void LogDiscoveryComplete(ILogger logger, int postCount, int draftsSkipped);

    /// <summary>Logs one post at debug.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="slug">URL-safe slug.</param>
    /// <param name="published">Publish date.</param>
    /// <param name="title">Post title.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovered post {Slug} ({Published}) — {Title}")]
    public static partial void LogPostDiscovered(ILogger logger, string slug, DateOnly published, string title);

    /// <summary>Logs the index/archive page generation summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="indexPath">Index page path.</param>
    /// <param name="archiveCount">Tag/category archive pages written.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Wrote blog index {IndexPath} and {ArchiveCount} archive page(s)")]
    public static partial void LogIndexGenerated(ILogger logger, string indexPath, int archiveCount);
}
