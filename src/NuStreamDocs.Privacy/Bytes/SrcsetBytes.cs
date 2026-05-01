// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Byte-level scanner that locates <c>srcset="..."</c> attributes,
/// splits the comma-delimited entries, and rewrites each entry's URL
/// to <c>/{local}</c> via the registry. Replaces <c>SrcsetRegex</c>.
/// </summary>
internal static class SrcsetBytes
{
    /// <summary>Bytes that may start a <c>srcset</c> attribute name (case-insensitive).</summary>
    private static readonly SearchValues<byte> AttrStart = SearchValues.Create("sS"u8);

    /// <summary>Gets the lowercase <c>srcset</c> attribute name.</summary>
    private static ReadOnlySpan<byte> Srcset => "srcset"u8;

    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every <c>srcset</c> attribute the registry localises.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink the rewritten output lands in.</param>
    /// <returns>True when at least one srcset was rewritten.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, in UrlRewriteContext ctx, IBufferWriter<byte> sink) =>
        UrlScanLoop.Run(html, AttrStart, sink, ctx, TryRewriteAt);

    /// <summary>Walks <paramref name="html"/> in audit mode, recording every srcset entry's URL.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="audit">Audit collector.</param>
    public static void AuditInto(ReadOnlySpan<byte> html, UrlAuditContext audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        UrlScanLoop.RunAudit(html, AttrStart, audit, TryAuditAt);
    }

    /// <summary>Tries to rewrite a srcset attribute starting at <paramref name="p"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when the srcset was rewritten with at least one localised URL.</returns>
    private static bool TryRewriteAt(ReadOnlySpan<byte> html, int p, in UrlRewriteContext ctx, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!TryMatchHeader(html, p, out var valueStart, out var valueEnd, out _))
        {
            advanceTo = p + 1;
            return false;
        }

        var value = html[valueStart..valueEnd];
        var temp = new ArrayBufferWriter<byte>(value.Length);
        var anyChanged = WriteRewrittenValue(value, ctx, temp);
        if (!anyChanged)
        {
            advanceTo = valueEnd + 1;
            return false;
        }

        sink.Write(html[lastEmit..valueStart]);
        sink.Write(temp.WrittenSpan);
        lastEmit = valueEnd;
        advanceTo = valueEnd;
        return true;
    }

    /// <summary>Audit-mode counterpart of <see cref="TryRewriteAt"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="audit">Audit collector.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when at least one srcset entry was inspected.</returns>
    private static bool TryAuditAt(ReadOnlySpan<byte> html, int p, UrlAuditContext audit, out int advanceTo)
    {
        if (!TryMatchHeader(html, p, out var valueStart, out var valueEnd, out _))
        {
            advanceTo = p + 1;
            return false;
        }

        var value = html[valueStart..valueEnd];
        var entryStart = 0;
        for (var i = 0; i <= value.Length; i++)
        {
            if (i != value.Length && value[i] is not (byte)',')
            {
                continue;
            }

            AuditOneEntry(value[entryStart..i], audit);
            entryStart = i + 1;
        }

        advanceTo = valueEnd + 1;
        return true;
    }

    /// <summary>Walks the comma-separated entries in <paramref name="value"/>, writing each rewritten entry into <paramref name="sink"/>.</summary>
    /// <param name="value">Raw srcset value.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one entry was localised.</returns>
    private static bool WriteRewrittenValue(ReadOnlySpan<byte> value, in UrlRewriteContext ctx, ArrayBufferWriter<byte> sink)
    {
        var anyChanged = false;
        var entryStart = 0;
        for (var i = 0; i <= value.Length; i++)
        {
            if (i < value.Length && value[i] is not (byte)',')
            {
                continue;
            }

            anyChanged |= WriteOneEntry(value[entryStart..i], ctx, sink);
            if (i < value.Length)
            {
                var dst = sink.GetSpan(1);
                dst[0] = (byte)',';
                sink.Advance(1);
            }

            entryStart = i + 1;
        }

        return anyChanged;
    }

    /// <summary>Writes one srcset entry, with the URL portion replaced by <c>/{local}</c> when the filter accepts it.</summary>
    /// <param name="entry">One <c>url descriptor</c> entry.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when localisation happened.</returns>
    private static bool WriteOneEntry(ReadOnlySpan<byte> entry, in UrlRewriteContext ctx, ArrayBufferWriter<byte> sink)
    {
        var leading = SkipLeadingWhitespace(entry);
        var urlEnd = FindUrlEnd(entry, leading);
        if (leading >= urlEnd)
        {
            sink.Write(entry);
            return false;
        }

        var url = Encoding.UTF8.GetString(entry[leading..urlEnd]);
        if (!ctx.Filter.ShouldLocalise(url))
        {
            sink.Write(entry);
            return false;
        }

        var local = ctx.Registry.GetOrAdd(url);
        sink.Write(entry[..leading]);
        var slash = sink.GetSpan(1);
        slash[0] = (byte)'/';
        sink.Advance(1);
        ByteHelpers.EncodeStringInto(local, sink);
        sink.Write(entry[urlEnd..]);
        return true;
    }

    /// <summary>Records the URL in <paramref name="entry"/> when the host filter accepts it.</summary>
    /// <param name="entry">One srcset entry.</param>
    /// <param name="audit">Audit collector.</param>
    private static void AuditOneEntry(ReadOnlySpan<byte> entry, UrlAuditContext audit)
    {
        var leading = SkipLeadingWhitespace(entry);
        var urlEnd = FindUrlEnd(entry, leading);
        if (leading >= urlEnd)
        {
            return;
        }

        var url = Encoding.UTF8.GetString(entry[leading..urlEnd]);
        if (!audit.Filter.ShouldLocalise(url))
        {
            return;
        }

        audit.Set.TryAdd(url, 0);
    }

    /// <summary>Validates <c>\bsrcset\s*=\s*("|')</c> at <paramref name="p"/> and returns the value byte range.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="valueStart">First value byte on success.</param>
    /// <param name="valueEnd">Offset of the closing quote on success.</param>
    /// <param name="quote">Closing-quote byte.</param>
    /// <returns>True on a successful match.</returns>
    private static bool TryMatchHeader(ReadOnlySpan<byte> html, int p, out int valueStart, out int valueEnd, out byte quote)
    {
        valueStart = -1;
        valueEnd = -1;
        quote = 0;
        if (!ByteHelpers.IsWordBoundary(html, p) || !ByteHelpers.StartsWithIgnoreAsciiCase(html, p, Srcset))
        {
            return false;
        }

        var afterEq = ByteHelpers.SkipWhitespace(html, p + Srcset.Length);
        if (afterEq >= html.Length || html[afterEq] is not (byte)'=')
        {
            return false;
        }

        var afterEq2 = ByteHelpers.SkipWhitespace(html, afterEq + 1);
        if (afterEq2 >= html.Length || html[afterEq2] is not ((byte)'"' or (byte)'\''))
        {
            return false;
        }

        quote = html[afterEq2];
        valueStart = afterEq2 + 1;
        var endRel = html[valueStart..].IndexOf(quote);
        if (endRel < 0)
        {
            return false;
        }

        valueEnd = valueStart + endRel;
        return true;
    }

    /// <summary>Returns the offset of the first non-whitespace byte in <paramref name="entry"/>.</summary>
    /// <param name="entry">Entry span.</param>
    /// <returns>Leading-whitespace count.</returns>
    private static int SkipLeadingWhitespace(ReadOnlySpan<byte> entry)
    {
        var leading = 0;
        while (leading < entry.Length && ByteHelpers.IsAsciiWhitespace(entry[leading]))
        {
            leading++;
        }

        return leading;
    }

    /// <summary>Finds the URL terminator (first whitespace byte) starting at <paramref name="from"/>.</summary>
    /// <param name="entry">Entry span.</param>
    /// <param name="from">Search start offset.</param>
    /// <returns>Offset just past the URL.</returns>
    private static int FindUrlEnd(ReadOnlySpan<byte> entry, int from)
    {
        var p = from;
        while (p < entry.Length && !ByteHelpers.IsAsciiWhitespace(entry[p]))
        {
            p++;
        }

        return p;
    }
}
