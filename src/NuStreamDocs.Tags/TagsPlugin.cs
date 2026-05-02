// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tags;

/// <summary>
/// Tags plugin. Scans each page's <c>tags:</c> frontmatter during
/// the build, collects tag → pages, and at finalize emits a
/// tags-index page (<c>tags/index.html</c>) plus one listing page
/// per distinct tag (<c>tags/{slug}.html</c>).
/// </summary>
/// <remarks>
/// The output is intentionally minimal HTML — sites can theme it by
/// styling the <c>.tags-index</c> / <c>.tags-page</c> classes from
/// their own stylesheet. The plugin doesn't inject anything into the
/// rendered page bodies; that's a theme-template concern.
/// </remarks>
public sealed class TagsPlugin : IDocPlugin
{
    /// <summary>Per-page entries collected during the build; drained at finalize time.</summary>
    private readonly ConcurrentQueue<TagEntry> _entries = [];

    /// <summary>Plugin options.</summary>
    private readonly TagsOptions _options;

    /// <summary>Initializes a new instance of the <see cref="TagsPlugin"/> class with default options.</summary>
    public TagsPlugin()
        : this(TagsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagsPlugin"/> class with caller-supplied options.</summary>
    /// <param name="options">Options controlling the output subdirectory and index slug.</param>
    public TagsPlugin(in TagsOptions options) => _options = options;

    /// <inheritdoc/>
    public string Name => "tags";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var tags = TagsFrontmatterReader.Read(context.Source.Span);
        if (tags.Length is 0)
        {
            return ValueTask.CompletedTask;
        }

        var url = TagsIndexWriter.RelativePathToUrlPath(context.RelativePath);
        var title = ExtractTitle(context.Html.WrittenSpan, fallback: url);
        for (var i = 0; i < tags.Length; i++)
        {
            _entries.Enqueue(new(tags[i], url, title));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_entries.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        TagEntry[] entries = [.. _entries];
        TagsIndexWriter.Write(context.OutputRoot, _options, entries);

        return ValueTask.CompletedTask;
    }

    /// <summary>Pulls the heading-1 text out of the rendered HTML to use as the page title; falls back to <paramref name="fallback"/>.</summary>
    /// <param name="html">UTF-8 rendered HTML.</param>
    /// <param name="fallback">Default UTF-8 title bytes when no <c>&lt;h1&gt;</c> is found.</param>
    /// <returns>The page title bytes.</returns>
    private static byte[] ExtractTitle(ReadOnlySpan<byte> html, byte[] fallback)
    {
        var openRel = html.IndexOf("<h1"u8);
        if (openRel < 0)
        {
            return fallback;
        }

        var closeRel = html[openRel..].IndexOf((byte)'>');
        if (closeRel < 0)
        {
            return fallback;
        }

        var bodyStart = openRel + closeRel + 1;
        var bodyEnd = html[bodyStart..].IndexOf("</h1>"u8);
        if (bodyEnd < 0)
        {
            return fallback;
        }

        var titleSpan = AsciiByteHelpers.TrimAsciiWhitespace(html.Slice(bodyStart, bodyEnd));
        return titleSpan.IsEmpty ? fallback : titleSpan.ToArray();
    }
}
