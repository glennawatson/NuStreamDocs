// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;
using static NuStreamDocs.Markdown.Common.MarkdownCodeScanner;

namespace NuStreamDocs.MagicLink;

/// <summary>
/// Stateless UTF-8 magic-link rewriter. Wraps bare URLs in
/// CommonMark autolink brackets so the downstream inline renderer
/// emits anchor tags. Skips fenced-code, inline-code, and any
/// already-linked region (<c>[…](…)</c> or <c>&lt;url&gt;</c>).
/// </summary>
internal static class MagicLinkRewriter
{
    /// <summary>Maximum length of a GitHub username (matches the platform's documented 39-character cap).</summary>
    private const int MaxGitHubUsernameLength = 39;

    /// <summary>Sentence-terminator bytes peeled off a permissive URL match.</summary>
    private static readonly SearchValues<byte> TrailingPunctuation = SearchValues.Create(".,;:!?)]}'*_"u8);

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/> with no shortref expansion.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        Rewrite(source, writer, default, false);

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/> with optional GitHub-shortref expansion.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="defaultRepo"><c>org/repo</c> bytes used to expand bare <c>#NNN</c> issue refs; empty disables that pass.</param>
    /// <param name="expandMentions">When true, <c>@user</c> mentions at word boundaries become <c>[@user](https://github.com/user)</c> Markdown links.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, ReadOnlySpan<byte> defaultRepo, bool expandMentions)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (AtLineStart(source, i) && TryConsumeFence(source, i, out var fenceEnd))
            {
                writer.Write(source[i..fenceEnd]);
                i = fenceEnd;
                continue;
            }

            switch (source[i])
            {
                case (byte)'`':
                    {
                        var inlineEnd = ConsumeInlineCode(source, i);
                        writer.Write(source[i..inlineEnd]);
                        i = inlineEnd;
                        continue;
                    }

                case (byte)'<':
                    {
                        // Already an autolink or HTML — skip to its closing '>' verbatim so
                        // we don't double-wrap or wander into a tag's attributes.
                        var skipEnd = ConsumeAngleSpan(source, i);
                        writer.Write(source[i..skipEnd]);
                        i = skipEnd;
                        continue;
                    }

                case (byte)'[':
                    {
                        // A markdown link's label may legally contain a URL; preserve the
                        // [...](...) span verbatim instead of rewriting inside it.
                        var skipEnd = ConsumeBracketSpan(source, i);
                        writer.Write(source[i..skipEnd]);
                        i = skipEnd;
                        continue;
                    }

                case (byte)'@' when expandMentions && TryRewriteUserMention(source, i, writer, out var mentionConsumed):
                    {
                        i += mentionConsumed;
                        continue;
                    }

                case (byte)'#' when defaultRepo.Length > 0 && TryRewriteIssueRef(source, i, defaultRepo, writer, out var issueConsumed):
                    {
                        i += issueConsumed;
                        continue;
                    }
            }

            if (TryRewriteUrl(source, i, writer, out var consumed))
            {
                i += consumed;
                continue;
            }

            var dest = writer.GetSpan(1);
            dest[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Tries to expand <c>@user</c> at <paramref name="offset"/> into a Markdown link.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the <c>@</c> byte.</param>
    /// <param name="writer">Sink for the rewritten span.</param>
    /// <param name="consumed">Number of input bytes covered.</param>
    /// <returns>True when a mention was rewritten.</returns>
    private static bool TryRewriteUserMention(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (!AsciiWordBoundary.IsBefore(source, offset))
        {
            return false;
        }

        var nameStart = offset + 1;
        var nameEnd = ScanUserName(source, nameStart);
        if (nameEnd == nameStart)
        {
            return false;
        }

        // GitHub usernames must start with an alphanumeric, can't end with a hyphen.
        var first = source[nameStart];
        if (first is (byte)'-')
        {
            return false;
        }

        var trimmed = nameEnd;
        while (trimmed > nameStart && source[trimmed - 1] is (byte)'-')
        {
            trimmed--;
        }

        if (trimmed == nameStart)
        {
            return false;
        }

        // Suppress mention rewriting when the name is followed by ':' — that shape (@autoref:T:Foo,
        // @user:host IRC handles, etc.) is reserved for non-mention markers we must leave alone.
        if (trimmed < source.Length && source[trimmed] is (byte)':')
        {
            return false;
        }

        writer.Write("[@"u8);
        writer.Write(source[nameStart..trimmed]);
        writer.Write("](https://github.com/"u8);
        writer.Write(source[nameStart..trimmed]);
        writer.Write(")"u8);
        consumed = trimmed - offset;
        return true;
    }

    /// <summary>Tries to expand <c>#NNN</c> at <paramref name="offset"/> into a Markdown issue link against <paramref name="defaultRepo"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the <c>#</c> byte.</param>
    /// <param name="defaultRepo"><c>org/repo</c> path bytes (no leading or trailing slash).</param>
    /// <param name="writer">Sink for the rewritten span.</param>
    /// <param name="consumed">Number of input bytes covered.</param>
    /// <returns>True when an issue ref was rewritten.</returns>
    private static bool TryRewriteIssueRef(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> defaultRepo, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (!AsciiWordBoundary.IsBefore(source, offset))
        {
            return false;
        }

        var numberStart = offset + 1;
        var numberEnd = ScanDigits(source, numberStart);
        if (numberEnd == numberStart)
        {
            return false;
        }

        // A valid GitHub issue / PR ref ends at a non-word byte.
        if (numberEnd < source.Length && IsUserNameByte(source[numberEnd]))
        {
            return false;
        }

        writer.Write("[#"u8);
        writer.Write(source[numberStart..numberEnd]);
        writer.Write("](https://github.com/"u8);
        writer.Write(defaultRepo);
        writer.Write("/issues/"u8);
        writer.Write(source[numberStart..numberEnd]);
        writer.Write(")"u8);
        consumed = numberEnd - offset;
        return true;
    }

    /// <summary>Scans forward over GitHub-username bytes (alphanumeric + hyphen) starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor position.</param>
    /// <returns>Exclusive end of the username span; capped at <see cref="MaxGitHubUsernameLength"/>.</returns>
    private static int ScanUserName(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        var max = Math.Min(source.Length, offset + MaxGitHubUsernameLength);
        while (p < max && IsUserNameByte(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Scans forward over ASCII digits starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor position.</param>
    /// <returns>Exclusive end of the digit run.</returns>
    private static int ScanDigits(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && source[p] is >= (byte)'0' and <= (byte)'9')
        {
            p++;
        }

        return p;
    }

    /// <summary>Returns true when <paramref name="b"/> is a permissible GitHub-username byte (ASCII alphanumeric or hyphen).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for username constituents.</returns>
    private static bool IsUserNameByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'-';

    /// <summary>Tries to wrap a bare URL starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor position.</param>
    /// <param name="writer">Sink for the autolink-wrapped URL.</param>
    /// <param name="consumed">Number of input bytes the match covered.</param>
    /// <returns>True when a URL was rewritten.</returns>
    private static bool TryRewriteUrl(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (!AsciiWordBoundary.IsBefore(source, offset))
        {
            return false;
        }

        var schemeLength = MatchScheme(source, offset);
        if (schemeLength is 0)
        {
            return false;
        }

        var afterScheme = offset + schemeLength;
        if (afterScheme >= source.Length || !IsUrlBodyStart(source[afterScheme]))
        {
            return false;
        }

        var bodyEnd = ScanUrlBody(source, afterScheme);
        var trimmed = TrimTrailingPunctuation(source, afterScheme, bodyEnd);
        if (trimmed <= afterScheme)
        {
            return false;
        }

        writer.Write("<"u8);
        writer.Write(source[offset..trimmed]);
        writer.Write(">"u8);
        consumed = trimmed - offset;
        return true;
    }

    /// <summary>Returns the byte length of the URL scheme starting at <paramref name="offset"/>; zero when no known scheme matches.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate start.</param>
    /// <returns>Scheme length including its delimiter.</returns>
    private static int MatchScheme(ReadOnlySpan<byte> source, int offset)
    {
        var slice = source[offset..];
        if (slice.StartsWith("https://"u8))
        {
            return "https://"u8.Length;
        }

        if (slice.StartsWith("http://"u8))
        {
            return "http://"u8.Length;
        }

        if (slice.StartsWith("ftps://"u8))
        {
            return "ftps://"u8.Length;
        }

        if (slice.StartsWith("ftp://"u8))
        {
            return "ftp://"u8.Length;
        }

        if (slice.StartsWith("mailto:"u8))
        {
            return "mailto:"u8.Length;
        }

        if (slice.StartsWith("www."u8))
        {
            return 0; // leave for now — bare `www.` autolinking needs scheme synthesis we don't yet do
        }

        return 0;
    }

    /// <summary>Returns true when <paramref name="b"/> can start a URL body.</summary>
    /// <param name="b">Byte just after the scheme.</param>
    /// <returns>True for a host/userinfo opener.</returns>
    private static bool IsUrlBodyStart(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'-' or (byte)'_' or (byte)'+' or (byte)'%';

    /// <summary>Scans forward over URL body bytes, returning the exclusive end.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position just past the scheme.</param>
    /// <returns>End of the URL body span.</returns>
    private static int ScanUrlBody(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && IsUrlBodyByte(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Returns true when <paramref name="b"/> is a permissible URL body byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True when the byte is part of a URL.</returns>
    private static bool IsUrlBodyByte(byte b) =>
        b is >= (byte)'!' and <= (byte)'~'
        and not (byte)'<' and not (byte)'>'
        and not (byte)'"' and not (byte)'\\'
        and not (byte)'`' and not (byte)' ';

    /// <summary>Trims trailing punctuation likely to be sentence terminators rather than URL parts.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="bodyStart">First byte of the URL body.</param>
    /// <param name="bodyEnd">Exclusive end after permissive scan.</param>
    /// <returns>Adjusted exclusive end.</returns>
    private static int TrimTrailingPunctuation(ReadOnlySpan<byte> source, int bodyStart, int bodyEnd)
    {
        var end = bodyEnd;
        while (end > bodyStart && TrailingPunctuation.Contains(source[end - 1]))
        {
            end--;
        }

        return end;
    }

    /// <summary>Consumes through a <c>&lt;…&gt;</c> span (autolink or HTML); advances past the closing <c>&gt;</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the opening <c>&lt;</c>.</param>
    /// <returns>Exclusive end after the close angle, or the input position + 1 when no close is found.</returns>
    private static int ConsumeAngleSpan(ReadOnlySpan<byte> source, int offset)
    {
        var rel = source[offset..].IndexOf((byte)'>');
        return rel < 0 ? offset + 1 : offset + rel + 1;
    }

    /// <summary>Consumes through a markdown link's bracket span — <c>[label](dest)</c> when present.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the opening <c>[</c>.</param>
    /// <returns>Exclusive end past the closing <c>)</c>, or the input position + 1 when the bracket is bare.</returns>
    /// <remarks>
    /// Uses depth-tracked matching for both the <c>[…]</c> label and the <c>(…)</c> destination so a label
    /// like <c>[IObservable&lt;byte[]?&gt;]</c> (which contains nested <c>[]</c>) does not split on its first
    /// inner <c>]</c> and leak the destination URL out for autolink rewriting.
    /// </remarks>
    private static int ConsumeBracketSpan(ReadOnlySpan<byte> source, int offset)
    {
        var labelClose = FindMatchingDepth(source, offset + 1, (byte)'[', (byte)']');
        if (labelClose < 0)
        {
            return offset + 1;
        }

        var afterLabel = labelClose + 1;
        if (afterLabel >= source.Length || source[afterLabel] is not (byte)'(')
        {
            return afterLabel;
        }

        var destClose = FindMatchingDepth(source, afterLabel + 1, (byte)'(', (byte)')');
        return destClose < 0 ? afterLabel : destClose + 1;
    }

    /// <summary>Returns the index of the matching <paramref name="close"/> for an already-consumed opener; -1 when unbalanced.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="searchFrom">First byte to consider (one past the opener).</param>
    /// <param name="open">Opener byte (e.g. <c>[</c> or <c>(</c>).</param>
    /// <param name="close">Closer byte (e.g. <c>]</c> or <c>)</c>).</param>
    /// <returns>Index of the matching closer, or -1.</returns>
    private static int FindMatchingDepth(ReadOnlySpan<byte> source, int searchFrom, byte open, byte close)
    {
        var depth = 1;
        for (var i = searchFrom; i < source.Length; i++)
        {
            var b = source[i];
            if (b == open)
            {
                depth++;
                continue;
            }

            if (b != close)
            {
                continue;
            }

            depth--;
            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }
}
