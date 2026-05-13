// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Emoji;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-lookup + per-rewrite cost of the emoji shortcode table after it moved from a ~430-arm
/// <c>switch</c> expression to a <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>
/// with a <c>ReadOnlySpan&lt;byte&gt;</c> alternate lookup. The hit / miss split pins both the
/// successful-resolve path and the fall-through that <c>:not_an_emoji:</c> tokens take.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class EmojiBenchmarks
{
    /// <summary>Headroom factor for the rewriter's output writer relative to the source length.</summary>
    private const int OutputExpansionFactor = 4;

    /// <summary>Known shortcodes spread across the table (start, middle, end) plus a few mixed-position ones.</summary>
    private static readonly byte[][] KnownShortcodes =
    [
        [.. "smile"u8],
        [.. "rocket"u8],
        [.. "heart"u8],
        [.. "thumbsup"u8],
        [.. "tada"u8],
        [.. "fire"u8],
        [.. "warning"u8],
        [.. "white_check_mark"u8],
        [.. "bug"u8],
        [.. "sparkles"u8],
        [.. "books"u8],
        [.. "bike"u8],
        [.. "soccer"u8],
        [.. "badminton"u8]
    ];

    /// <summary>Unknown shortcodes — none of these are in the table, so every lookup falls through.</summary>
    private static readonly byte[][] UnknownShortcodes =
    [
        [.. "definitely_not_an_emoji"u8],
        [.. "smilezzz"u8],
        [.. "x"u8],
        [.. "rockett"u8],
        [.. "longish_made_up_name"u8],
        [.. "z"u8],
        [.. "almost_real"u8],
        [.. "nope"u8]
    ];

    /// <summary>Markdown body sprinkled with both known and unknown <c>:shortcode:</c> tokens plus a code span the rewriter must skip.</summary>
    private static readonly byte[] MarkdownWithShortcodes =
    [
        .. "Ship it :rocket: — the build is green :white_check_mark: and the tests pass :tada:.\n\n"u8,
        .. "Watch out for :bug: reports though, and `:this_one_is_in_code:` should be left alone. "u8,
        .. "We :heart: this :sparkles: release :fire: but :not_a_real_emoji: stays as-is. Add :books: to the docs. :thumbsup:\n"u8
    ];

    /// <summary>Reusable output writer for the rewriter benchmark.</summary>
    private readonly ArrayBufferWriter<byte> _writer = new(MarkdownWithShortcodes.Length * OutputExpansionFactor);

    /// <summary>Resolves every known shortcode against the table.</summary>
    /// <returns>The total resolved glyph byte length (kept to defeat dead-code elimination).</returns>
    [Benchmark]
    public int LookupHits()
    {
        var total = 0;
        for (var i = 0; i < KnownShortcodes.Length; i++)
        {
            if (EmojiIndex.TryGet(KnownShortcodes[i], out var glyph))
            {
                total += glyph.Length;
            }
        }

        return total;
    }

    /// <summary>Looks up every unknown shortcode (each falls through the table).</summary>
    /// <returns>The count of shortcodes that resolved (always zero; kept to defeat dead-code elimination).</returns>
    [Benchmark]
    public int LookupMisses()
    {
        var resolved = 0;
        for (var i = 0; i < UnknownShortcodes.Length; i++)
        {
            if (EmojiIndex.TryGet(UnknownShortcodes[i], out _))
            {
                resolved++;
            }
        }

        return resolved;
    }

    /// <summary>Runs the full code-aware rewriter over a body with mixed known / unknown shortcodes.</summary>
    /// <returns>The rewritten byte length.</returns>
    [Benchmark]
    public int RewriteMarkdown()
    {
        _writer.ResetWrittenCount();
        EmojiRewriter.Rewrite(MarkdownWithShortcodes, _writer);
        return _writer.WrittenCount;
    }
}
