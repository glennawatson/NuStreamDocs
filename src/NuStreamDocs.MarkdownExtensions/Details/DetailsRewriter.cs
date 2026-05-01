// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.Details;

/// <summary>
/// Stateless UTF-8 details-block rewriter. Scans <c>??? type "title"</c>
/// or <c>???+ type "title"</c> openers and emits the matching
/// <c>&lt;details&gt;</c> element.
/// </summary>
internal static class DetailsRewriter
{
    /// <summary>Gets the collapsed-opener marker.</summary>
    private static ReadOnlySpan<byte> Opener => "??? "u8;

    /// <summary>Gets the open-by-default opener marker.</summary>
    private static ReadOnlySpan<byte> OpenerOpen => "???+ "u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i)
                && TryParseOpener(source, i, out var opener))
            {
                var bodyEnd = IndentedBlockScanner.ConsumeBody(source, opener.HeaderEnd);
                EmitDetails(
                    opener.OpenByDefault,
                    source.Slice(opener.TypeStart, opener.TypeLen),
                    opener.TitleLen is 0 ? default : source.Slice(opener.TitleStart, opener.TitleLen),
                    source[opener.HeaderEnd..bodyEnd],
                    writer);
                i = bodyEnd;
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, i);
            writer.Write(source[i..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Tries to parse a details opener starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="offset">Candidate line-start offset.</param>
    /// <param name="opener">Parsed opener info on success.</param>
    /// <returns>True when <paramref name="offset"/> begins a valid opener.</returns>
    private static bool TryParseOpener(ReadOnlySpan<byte> source, int offset, out OpenerInfo opener)
    {
        opener = default;

        bool openByDefault;
        ReadOnlySpan<byte> marker;
        if (offset + OpenerOpen.Length <= source.Length && source[offset..].StartsWith(OpenerOpen))
        {
            openByDefault = true;
            marker = OpenerOpen;
        }
        else if (offset + Opener.Length <= source.Length && source[offset..].StartsWith(Opener))
        {
            openByDefault = false;
            marker = Opener;
        }
        else
        {
            return false;
        }

        if (!OpenerLineParser.TryParseTypeAndTitle(
            source,
            offset + marker.Length,
            out var typeStart,
            out var typeLen,
            out var titleStart,
            out var titleLen,
            out var headerEnd))
        {
            return false;
        }

        opener = new(openByDefault, typeStart, typeLen, titleStart, titleLen, headerEnd);
        return true;
    }

    /// <summary>Emits one <c>&lt;details&gt;</c> element.</summary>
    /// <param name="openByDefault">Whether to emit the <c>open</c> attribute.</param>
    /// <param name="type">UTF-8 type token used as a class name.</param>
    /// <param name="title">UTF-8 title bytes; empty when absent.</param>
    /// <param name="body">UTF-8 body bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitDetails(bool openByDefault, ReadOnlySpan<byte> type, ReadOnlySpan<byte> title, ReadOnlySpan<byte> body, IBufferWriter<byte> writer)
    {
        writer.Write("\n<details class=\""u8);
        writer.Write(type);
        writer.Write(openByDefault ? "\" open>\n<summary>"u8 : "\">\n<summary>"u8);
        if (title.Length is 0)
        {
            OpenerLineParser.WriteTitleCase(type, writer);
        }
        else
        {
            HtmlEscaper.Escape(title, writer);
        }

        writer.Write("</summary>\n\n"u8);
        IndentedBlockScanner.WriteDeindented(body, writer);
        writer.Write("\n</details>\n\n"u8);
    }

    /// <summary>Result tuple for a parsed details opener.</summary>
    /// <param name="OpenByDefault">True when the opener used <c>???+</c>.</param>
    /// <param name="TypeStart">Offset of the type token.</param>
    /// <param name="TypeLen">Length of the type token.</param>
    /// <param name="TitleStart">Offset of the title token (zero when absent).</param>
    /// <param name="TitleLen">Length of the title token (zero when absent).</param>
    /// <param name="HeaderEnd">Offset just past the opener line's terminator.</param>
    private readonly record struct OpenerInfo(
        bool OpenByDefault,
        int TypeStart,
        int TypeLen,
        int TitleStart,
        int TitleLen,
        int HeaderEnd);
}
