// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;
using NuStreamDocs.Feed.Logging;

namespace NuStreamDocs.Feed;

/// <summary>
/// Stateless emitter that drops the configured feed files into a
/// caller-supplied output directory. Pulled out of
/// <see cref="FeedPlugin"/> so the file-write path can be unit-tested
/// without standing up a whole plugin lifecycle.
/// </summary>
internal static class FeedEmitter
{
    /// <summary>RSS output filename.</summary>
    public const string RssFileName = "feed.xml";

    /// <summary>Atom output filename.</summary>
    public const string AtomFileName = "atom.xml";

    /// <summary>Pre-built RSS request descriptor.</summary>
    private static readonly FeedFormatRequest RssRequest =
        new(RssFileName, "RSS", FeedFormats.Rss, FeedWriter.WriteRss);

    /// <summary>Pre-built Atom request descriptor.</summary>
    private static readonly FeedFormatRequest AtomRequest =
        new(AtomFileName, "Atom", FeedFormats.Atom, FeedWriter.WriteAtom);

    /// <summary>Writes whichever feed formats are enabled in <paramref name="options"/> into <paramref name="outputDir"/>.</summary>
    /// <param name="options">Feed options (must already be valid).</param>
    /// <param name="outputDir">Absolute output directory (already created by the caller).</param>
    /// <param name="posts">Posts to render.</param>
    /// <param name="generatedAt">Generation timestamp.</param>
    /// <param name="logger">Logger for per-format diagnostics.</param>
    /// <returns>The set of formats actually written.</returns>
    public static FeedFormats WriteEnabledFormats(
        FeedOptions options,
        string outputDir,
        BlogPost[] posts,
        in DateTimeOffset generatedAt,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(outputDir);
        ArgumentNullException.ThrowIfNull(posts);
        ArgumentNullException.ThrowIfNull(logger);

        var written = FeedFormats.None;
        if (WriteIfRequested(options, outputDir, posts, generatedAt, logger, RssRequest))
        {
            written |= FeedFormats.Rss;
        }

        if (WriteIfRequested(options, outputDir, posts, generatedAt, logger, AtomRequest))
        {
            written |= FeedFormats.Atom;
        }

        return written;
    }

    /// <summary>Renders one feed format when its flag is set in <paramref name="options"/>.</summary>
    /// <param name="options">Feed options.</param>
    /// <param name="outputDir">Output directory.</param>
    /// <param name="posts">Posts list.</param>
    /// <param name="generatedAt">Generation timestamp.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="request">Per-format request (filename, label, flag, renderer).</param>
    /// <returns>True when the format was written.</returns>
    private static bool WriteIfRequested(
        FeedOptions options,
        string outputDir,
        BlogPost[] posts,
        in DateTimeOffset generatedAt,
        ILogger logger,
        in FeedFormatRequest request)
    {
        if ((options.Formats & request.Flag) != request.Flag)
        {
            return false;
        }

        var path = Path.Combine(outputDir, request.FileName);
        FeedLoggingHelper.LogFeedWriteStart(logger, request.FormatName, path);
        var bytes = request.Render(options, posts, generatedAt);
        File.WriteAllBytes(path, bytes);
        FeedLoggingHelper.LogFeedWriteComplete(logger, request.FormatName, posts.Length, bytes.Length);
        return true;
    }

    /// <summary>One feed format's rendering instructions.</summary>
    /// <param name="FileName">Output filename relative to <c>outputDir</c>.</param>
    /// <param name="FormatName">Human-readable format label used in log messages.</param>
    /// <param name="Flag">Feed-format flag tested against <see cref="FeedOptions.Formats"/>.</param>
    /// <param name="Render">Format-specific renderer.</param>
    private readonly record struct FeedFormatRequest(
        string FileName,
        string FormatName,
        FeedFormats Flag,
        Func<FeedOptions, BlogPost[], DateTimeOffset, byte[]> Render);
}
