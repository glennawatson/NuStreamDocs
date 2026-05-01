// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// UTF-8 byte scanner that upgrades <c>http://</c> URLs in
/// <c>src</c> / <c>href</c> attributes to <c>https://</c>, except for
/// loopback hosts. Replaces the regex-based
/// <c>HttpAttributeRegex</c> pass.
/// </summary>
internal static class MixedContentBytes
{
    /// <summary>Bytes that may start a <c>src</c> or <c>href</c> attribute name (case-insensitive).</summary>
    private static readonly SearchValues<byte> AttrStart = SearchValues.Create("sShH"u8);

    /// <summary>Gets UTF-8 bytes for <c>http://</c>.</summary>
    private static ReadOnlySpan<byte> Http => "http://"u8;

    /// <summary>Gets UTF-8 bytes for <c>https://</c>.</summary>
    private static ReadOnlySpan<byte> Https => "https://"u8;

    /// <summary>Gets UTF-8 bytes for <c>localhost</c>.</summary>
    private static ReadOnlySpan<byte> Localhost => "localhost"u8;

    /// <summary>Gets UTF-8 bytes for the IPv4 loopback prefix.</summary>
    private static ReadOnlySpan<byte> Ipv4Loopback => "127."u8;

    /// <summary>Gets UTF-8 bytes for the bare IPv6 loopback host.</summary>
    private static ReadOnlySpan<byte> Ipv6LoopbackBare => "::1"u8;

    /// <summary>Gets UTF-8 bytes for the bracketed IPv6 loopback host.</summary>
    private static ReadOnlySpan<byte> Ipv6LoopbackBracketed => "[::1]"u8;

    /// <summary>Gets UTF-8 bytes for the <c>src</c> attribute name.</summary>
    private static ReadOnlySpan<byte> Src => "src"u8;

    /// <summary>Gets UTF-8 bytes for the <c>href</c> attribute name.</summary>
    private static ReadOnlySpan<byte> Href => "href"u8;

    /// <summary>
    /// Walks <paramref name="html"/>, copying through verbatim, but
    /// rewriting every <c>src=</c> / <c>href=</c> attribute that
    /// references a non-loopback <c>http://</c> URL into <c>https://</c>.
    /// </summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="sink">UTF-8 sink the rewritten output lands in.</param>
    /// <returns>True when at least one URL was upgraded; false when the input passed through unchanged.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var changed = false;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOfAny(AttrStart);
            if (rel < 0)
            {
                break;
            }

            var p = cursor + rel;
            if (TryRewriteAt(html, p, sink, ref lastEmit, out var advanceTo))
            {
                changed = true;
                cursor = advanceTo;
                continue;
            }

            cursor = advanceTo > p ? advanceTo : p + 1;
        }

        if (!changed)
        {
            return false;
        }

        sink.Write(html[lastEmit..]);
        return true;
    }

    /// <summary>Tries to rewrite a <c>http://</c> attribute starting at <paramref name="p"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate attribute name offset.</param>
    /// <param name="sink">Output sink.</param>
    /// <param name="lastEmit">Offset up to which the source has been emitted.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when an attribute was rewritten.</returns>
    private static bool TryRewriteAt(ReadOnlySpan<byte> html, int p, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!TryMatchAttributeHeader(html, p, out var afterQuote))
        {
            advanceTo = afterQuote > p ? afterQuote : p + 1;
            return false;
        }

        var hostStart = afterQuote + Http.Length;
        var hostEnd = ScanHost(html, hostStart);
        if (IsLoopbackHost(html[hostStart..hostEnd]))
        {
            advanceTo = hostEnd;
            return false;
        }

        sink.Write(html[lastEmit..afterQuote]);
        sink.Write(Https);
        lastEmit = afterQuote + Http.Length;
        advanceTo = lastEmit;
        return true;
    }

    /// <summary>
    /// Validates the attribute header pattern <c>(src|href)=("|')http://</c>
    /// at <paramref name="p"/>, returning the offset just after the opening
    /// quote so the caller can validate the host.
    /// </summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="afterQuote">Offset of the first byte of the URL on success; otherwise the offset to advance the scan past.</param>
    /// <returns>True when a candidate <c>http://</c> attribute starts at <paramref name="p"/>.</returns>
    private static bool TryMatchAttributeHeader(ReadOnlySpan<byte> html, int p, out int afterQuote)
    {
        afterQuote = p + 1;
        if (!ByteHelpers.IsWordBoundary(html, p) || !TryMatchSrcOrHref(html, p, out var attrLen))
        {
            return false;
        }

        var afterAttr = p + attrLen;
        if (afterAttr >= html.Length || html[afterAttr] is not (byte)'=')
        {
            afterQuote = afterAttr;
            return false;
        }

        var afterEq = afterAttr + 1;
        if (afterEq >= html.Length || html[afterEq] is not ((byte)'"' or (byte)'\''))
        {
            afterQuote = afterEq;
            return false;
        }

        afterQuote = afterEq + 1;
        return afterQuote + Http.Length <= html.Length && html[afterQuote..].StartsWith(Http);
    }

    /// <summary>Tries to match either <c>src</c> or <c>href</c> at <paramref name="offset"/> case-insensitively.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <param name="length">Matched attribute name length on success.</param>
    /// <returns>True when one of the two names matched.</returns>
    private static bool TryMatchSrcOrHref(ReadOnlySpan<byte> source, int offset, out int length)
    {
        if (ByteHelpers.StartsWithIgnoreAsciiCase(source, offset, Src))
        {
            length = Src.Length;
            return true;
        }

        if (ByteHelpers.StartsWithIgnoreAsciiCase(source, offset, Href))
        {
            length = Href.Length;
            return true;
        }

        length = 0;
        return false;
    }

    /// <summary>Scans the host portion (bytes that are not <c>/</c>, <c>"</c>, <c>'</c>, <c>?</c>, or <c>#</c>).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Host start offset.</param>
    /// <returns>Offset of the first byte after the host.</returns>
    private static int ScanHost(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length)
        {
            var b = source[p];
            if (b is (byte)'/' or (byte)'"' or (byte)'\'' or (byte)'?' or (byte)'#')
            {
                break;
            }

            p++;
        }

        return p;
    }

    /// <summary>Returns true when <paramref name="host"/> is a loopback address that browsers exempt from mixed-content rules.</summary>
    /// <param name="host">Host bytes.</param>
    /// <returns>True for loopback.</returns>
    private static bool IsLoopbackHost(ReadOnlySpan<byte> host)
    {
        if (host.Length is 0)
        {
            return false;
        }

        if (ByteHelpers.EqualsIgnoreAsciiCase(host, Localhost))
        {
            return true;
        }

        if (host.StartsWith(Ipv4Loopback))
        {
            return true;
        }

        return host.SequenceEqual(Ipv6LoopbackBare) || host.SequenceEqual(Ipv6LoopbackBracketed);
    }
}
