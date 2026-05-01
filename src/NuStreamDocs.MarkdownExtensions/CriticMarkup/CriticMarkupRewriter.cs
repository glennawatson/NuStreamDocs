// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.CriticMarkup;

/// <summary>
/// Stateless UTF-8 CriticMarkup rewriter. Replaces <c>{++ … ++}</c>
/// / <c>{-- … --}</c> / <c>{~~ old ~&gt; new ~~}</c> /
/// <c>{== … ==}</c> / <c>{&gt;&gt; … &lt;&lt;}</c> spans with the
/// HTML pymdownx.critic produces.
/// </summary>
internal static class CriticMarkupRewriter
{
    /// <summary>Width of the opening <c>{xx</c> stub (e.g. <c>{++</c>) and the closing <c>xx}</c> tail.</summary>
    private const int MarkerLength = 3;

    /// <summary>Length of the <c>~&gt;</c> substitute-arrow used inside <c>{~~old~&gt;new~~}</c>.</summary>
    private const int SubstituteArrowLength = 2;

    /// <summary>Offset of the trailing <c>}</c> within a close-marker triple (e.g. <c>++}</c>).</summary>
    private const int CloseBraceOffset = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        CodeAwareRewriter.Run(source, writer, TryRewriteSpan);

    /// <summary>Tries to match a CriticMarkup span at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a span was emitted.</returns>
    private static bool TryRewriteSpan(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (source[offset] is not (byte)'{' || offset + MarkerLength >= source.Length)
        {
            return false;
        }

        var marker = MatchMarker(source, offset);
        if (marker is CriticMarker.None)
        {
            return false;
        }

        var contentStart = offset + MarkerLength;
        if (!TryFindClose(source, contentStart, marker, out var contentEnd))
        {
            return false;
        }

        EmitSpan(source[contentStart..contentEnd], marker, writer);
        consumed = contentEnd + MarkerLength - offset;
        return true;
    }

    /// <summary>Matches the two-byte critic-markup marker just after the opening <c>{</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor on the opening <c>{</c>.</param>
    /// <returns>The marker class, or <see cref="CriticMarker.None"/> when no match.</returns>
    private static CriticMarker MatchMarker(ReadOnlySpan<byte> source, int offset) =>
        (source[offset + 1], source[offset + CloseBraceOffset]) switch
        {
            ((byte)'+', (byte)'+') => CriticMarker.Insert,
            ((byte)'-', (byte)'-') => CriticMarker.Delete,
            ((byte)'~', (byte)'~') => CriticMarker.Substitute,
            ((byte)'=', (byte)'=') => CriticMarker.Highlight,
            ((byte)'>', (byte)'>') => CriticMarker.Comment,
            _ => CriticMarker.None,
        };

    /// <summary>Searches for the closing marker matching <paramref name="marker"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Offset just past the opening marker.</param>
    /// <param name="marker">Marker class to close.</param>
    /// <param name="contentEnd">Offset of the first close-marker byte on success.</param>
    /// <returns>True when a closer was found.</returns>
    private static bool TryFindClose(ReadOnlySpan<byte> source, int start, CriticMarker marker, out int contentEnd)
    {
        contentEnd = 0;
        var (a, b) = ClosePairFor(marker);
        for (var p = start; p + CloseBraceOffset < source.Length; p++)
        {
            if (source[p] == a && source[p + 1] == b && source[p + CloseBraceOffset] is (byte)'}')
            {
                contentEnd = p;
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the two-byte close pair preceding the trailing <c>}</c> for the given marker.</summary>
    /// <param name="marker">Marker class.</param>
    /// <returns>The pair of bytes that prefix the closing brace.</returns>
    private static (byte First, byte Second) ClosePairFor(CriticMarker marker) => marker switch
    {
        CriticMarker.Insert => ((byte)'+', (byte)'+'),
        CriticMarker.Delete => ((byte)'-', (byte)'-'),
        CriticMarker.Substitute => ((byte)'~', (byte)'~'),
        CriticMarker.Highlight => ((byte)'=', (byte)'='),
        _ => ((byte)'<', (byte)'<'),
    };

    /// <summary>Emits the HTML for a single CriticMarkup span.</summary>
    /// <param name="content">Inner bytes between the open and close markers.</param>
    /// <param name="marker">Marker class.</param>
    /// <param name="writer">Sink.</param>
    private static void EmitSpan(ReadOnlySpan<byte> content, CriticMarker marker, IBufferWriter<byte> writer)
    {
        if (marker is CriticMarker.Substitute)
        {
            EmitSubstitution(content, writer);
            return;
        }

        if (marker is CriticMarker.Comment)
        {
            writer.Write("<span class=\"critic comment\">"u8);
            writer.Write(content);
            writer.Write("</span>"u8);
            return;
        }

        writer.Write(OpenTagFor(marker));
        writer.Write(content);
        writer.Write(CloseTagFor(marker));
    }

    /// <summary>Renders a <c>{~~old~&gt;new~~}</c> substitution as <c>&lt;del&gt;old&lt;/del&gt;&lt;ins&gt;new&lt;/ins&gt;</c>.</summary>
    /// <param name="content">Inner bytes between the <c>{~~</c> and <c>~~}</c> markers.</param>
    /// <param name="writer">Sink.</param>
    private static void EmitSubstitution(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
    {
        var arrow = content.IndexOf("~>"u8);
        if (arrow < 0)
        {
            // Bare {~~text~~} with no arrow — render as a plain delete.
            writer.Write("<del>"u8);
            writer.Write(content);
            writer.Write("</del>"u8);
            return;
        }

        writer.Write("<del>"u8);
        writer.Write(content[..arrow]);
        writer.Write("</del><ins>"u8);
        writer.Write(content[(arrow + SubstituteArrowLength)..]);
        writer.Write("</ins>"u8);
    }

    /// <summary>Returns the open tag for non-substitute markers.</summary>
    /// <param name="marker">Marker class.</param>
    /// <returns>UTF-8 open tag bytes.</returns>
    private static ReadOnlySpan<byte> OpenTagFor(CriticMarker marker) => marker switch
    {
        CriticMarker.Insert => "<ins>"u8,
        CriticMarker.Delete => "<del>"u8,
        _ => "<mark>"u8,
    };

    /// <summary>Returns the close tag for non-substitute markers.</summary>
    /// <param name="marker">Marker class.</param>
    /// <returns>UTF-8 close tag bytes.</returns>
    private static ReadOnlySpan<byte> CloseTagFor(CriticMarker marker) => marker switch
    {
        CriticMarker.Insert => "</ins>"u8,
        CriticMarker.Delete => "</del>"u8,
        _ => "</mark>"u8,
    };
}
