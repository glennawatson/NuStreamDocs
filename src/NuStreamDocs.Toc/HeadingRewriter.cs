// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Toc;

/// <summary>
/// Rewrites a UTF-8 HTML snapshot, splicing <c>id="slug"</c> attributes
/// into heading open tags and inserting a permalink anchor right
/// before the matching <c>&lt;/hN&gt;</c>.
/// </summary>
/// <remarks>
/// The rewriter walks the heading list in order and copies the spans
/// between them straight from the snapshot, so untouched regions of
/// the page (paragraphs, lists, code blocks) never round-trip through
/// a <see cref="StringBuilder"/>.
/// </remarks>
internal static class HeadingRewriter
{
    /// <summary>Gets the rendered permalink anchor prefix.</summary>
    private static ReadOnlySpan<byte> PermalinkPrefix => "<a class=\"headerlink\" href=\"#"u8;

    /// <summary>Gets the bytes that terminate the permalink anchor open tag.</summary>
    private static ReadOnlySpan<byte> PermalinkMid => "\" title=\"Permanent link\">"u8;

    /// <summary>Gets the permalink anchor close tag.</summary>
    private static ReadOnlySpan<byte> PermalinkSuffix => "</a>"u8;

    /// <summary>Gets the <c> id="</c> attribute prefix injected into open tags.</summary>
    private static ReadOnlySpan<byte> IdAttrPrefix => " id=\""u8;

    /// <summary>Gets the close-quote for the injected <c>id</c> attribute.</summary>
    private static ReadOnlySpan<byte> IdAttrSuffix => "\""u8;

    /// <summary>Writes the rewritten body of <paramref name="snapshot"/> into <paramref name="writer"/>.</summary>
    /// <param name="snapshot">Original HTML bytes.</param>
    /// <param name="headings">Headings the scanner returned, with slugs assigned.</param>
    /// <param name="permalinkSymbol">Glyph rendered inside the permalink anchor.</param>
    /// <param name="writer">Target buffer writer.</param>
    public static void Rewrite(
        ReadOnlySpan<byte> snapshot,
        Heading[] headings,
        string permalinkSymbol,
        IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(headings);
        ArgumentNullException.ThrowIfNull(writer);

        if (headings.Length is 0)
        {
            Write(writer, snapshot);
            return;
        }

        var permalinkBytes = Encoding.UTF8.GetBytes(permalinkSymbol);
        var cursor = 0;
        for (var i = 0; i < headings.Length; i++)
        {
            var h = headings[i];

            // Copy span before the heading.
            Write(writer, snapshot[cursor..h.OpenTagStart]);

            // Emit the rewritten open tag.
            EmitOpenTag(writer, snapshot[h.OpenTagStart..h.OpenTagEnd], h);

            // Copy inner text up to the close tag.
            Write(writer, snapshot[h.TextStart..h.CloseTagStart]);

            // Emit permalink anchor.
            Write(writer, PermalinkPrefix);
            Utf8StringWriter.Write(writer, h.Slug);
            Write(writer, PermalinkMid);
            Write(writer, permalinkBytes);
            Write(writer, PermalinkSuffix);

            // Close tag itself ("</hN>") is 5 bytes; copy it verbatim.
            var closeEnd = h.CloseTagStart + 5;
            Write(writer, snapshot[h.CloseTagStart..closeEnd]);
            cursor = closeEnd;
        }

        // Trailing content after the last heading.
        Write(writer, snapshot[cursor..]);
    }

    /// <summary>Emits an open tag with an injected <c>id</c> attribute (or the original when one was already present).</summary>
    /// <param name="writer">Target writer.</param>
    /// <param name="openTag">Original open-tag bytes.</param>
    /// <param name="heading">Heading record carrying the resolved slug.</param>
    private static void EmitOpenTag(IBufferWriter<byte> writer, ReadOnlySpan<byte> openTag, in Heading heading)
    {
        if (heading.ExistingId is { Length: > 0 })
        {
            // Existing id was preserved verbatim — emit as-is.
            Write(writer, openTag);
            return;
        }

        // Insert ` id="slug"` immediately after `<hN`.
        const int TagPrefixLength = 3; // "<hN"
        Write(writer, openTag[..TagPrefixLength]);
        Write(writer, IdAttrPrefix);
        Utf8StringWriter.Write(writer, heading.Slug);
        Write(writer, IdAttrSuffix);
        Write(writer, openTag[TagPrefixLength..]);
    }

    /// <summary>Bulk-write a span of bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target writer.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
