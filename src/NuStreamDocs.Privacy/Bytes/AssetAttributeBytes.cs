// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Byte-level scanner that locates <c>src=</c> / <c>href=</c>
/// attributes with absolute http(s) URLs and either rewrites them to
/// <c>/{local}</c> via the registry or records them for audit.
/// Replaces <c>AssetAttributeRegex</c>.
/// </summary>
internal static class AssetAttributeBytes
{
    /// <summary>Bytes that may start a <c>src</c> or <c>href</c> attribute name (case-insensitive).</summary>
    private static readonly SearchValues<byte> AttrStart = SearchValues.Create("sShH"u8);

    /// <summary>Bytes that terminate a URL value scan (whitespace + reserved structural bytes).</summary>
    private static readonly SearchValues<byte> UrlTerminators = SearchValues.Create(" \t\r\n>\"'"u8);

    /// <summary>Gets the lowercase <c>src</c> attribute name.</summary>
    private static ReadOnlySpan<byte> Src => "src"u8;

    /// <summary>Gets the lowercase <c>href</c> attribute name.</summary>
    private static ReadOnlySpan<byte> Href => "href"u8;

    /// <summary>Gets the lowercase <c>http://</c> scheme.</summary>
    private static ReadOnlySpan<byte> HttpScheme => "http://"u8;

    /// <summary>Gets the lowercase <c>https://</c> scheme.</summary>
    private static ReadOnlySpan<byte> HttpsScheme => "https://"u8;

    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every <c>src</c>/<c>href</c> URL the registry localises.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="ctx">URL-rewrite context (filter + registry).</param>
    /// <param name="sink">UTF-8 sink the rewritten output lands in.</param>
    /// <returns>True when at least one URL was rewritten; false when the input passed through unchanged.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, in UrlRewriteContext ctx, IBufferWriter<byte> sink) =>
        UrlScanLoop.Run(html, AttrStart, sink, ctx, TryRewriteAt);

    /// <summary>Walks <paramref name="html"/> in audit mode, recording matched URLs in <paramref name="audit"/> without modifying the page.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="audit">Audit collector.</param>
    public static void AuditInto(ReadOnlySpan<byte> html, UrlAuditContext audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        UrlScanLoop.RunAudit(html, AttrStart, audit, TryAuditAt);
    }

    /// <summary>Attempts to rewrite an asset-attribute URL at <paramref name="p"/>; emits prefix + rewritten URL into <paramref name="sink"/> when localised.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Offset up to which the source has been emitted.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when a URL was rewritten.</returns>
    private static bool TryRewriteAt(ReadOnlySpan<byte> html, int p, in UrlRewriteContext ctx, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!TryMatchHeader(html, p, out var urlStart, out var urlEnd, out var quote))
        {
            advanceTo = p + 1;
            return false;
        }

        var url = Encoding.UTF8.GetString(html[urlStart..urlEnd]);
        if (!ctx.Filter.ShouldLocalise(url))
        {
            advanceTo = urlEnd;
            return false;
        }

        var local = ctx.Registry.GetOrAdd(url);
        sink.Write(html[lastEmit..urlStart]);
        var dst = sink.GetSpan(1);
        dst[0] = (byte)'/';
        sink.Advance(1);
        ByteHelpers.EncodeStringInto(local, sink);
        lastEmit = urlEnd;
        advanceTo = urlEnd;
        _ = quote;
        return true;
    }

    /// <summary>Audit-mode counterpart of <see cref="TryRewriteAt"/> — records the URL when the filter accepts it.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="audit">Audit collector.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when a URL was recorded (always advances scan past the URL).</returns>
    private static bool TryAuditAt(ReadOnlySpan<byte> html, int p, UrlAuditContext audit, out int advanceTo)
    {
        if (!TryMatchHeader(html, p, out var urlStart, out var urlEnd, out _))
        {
            advanceTo = p + 1;
            return false;
        }

        var url = Encoding.UTF8.GetString(html[urlStart..urlEnd]);
        if (audit.Filter.ShouldLocalise(url))
        {
            audit.Set.TryAdd(url, 0);
        }

        advanceTo = urlEnd;
        return true;
    }

    /// <summary>Validates <c>(src|href)\s*=\s*("|')https?://[^"'\s>]+("|')</c> at <paramref name="p"/>, returning the URL byte range and quote byte on success.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="urlStart">First URL byte offset on success.</param>
    /// <param name="urlEnd">Offset just past the last URL byte on success.</param>
    /// <param name="quote">Quote byte that terminates the value.</param>
    /// <returns>True on a successful match.</returns>
    private static bool TryMatchHeader(ReadOnlySpan<byte> html, int p, out int urlStart, out int urlEnd, out byte quote)
    {
        urlStart = -1;
        urlEnd = -1;
        quote = 0;

        if (!ByteHelpers.IsWordBoundary(html, p))
        {
            return false;
        }

        if (!TryAttrName(html, p, out var afterName))
        {
            return false;
        }

        var afterEq = ByteHelpers.SkipWhitespace(html, afterName);
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
        urlStart = afterEq2 + 1;
        return TryScanUrl(html, urlStart, quote, out urlEnd);
    }

    /// <summary>Tries to match <c>src</c> or <c>href</c> at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <param name="afterName">Offset past the matched name on success.</param>
    /// <returns>True when one of the two names matched.</returns>
    private static bool TryAttrName(ReadOnlySpan<byte> source, int offset, out int afterName)
    {
        if (ByteHelpers.StartsWithIgnoreAsciiCase(source, offset, Src))
        {
            afterName = offset + Src.Length;
            return true;
        }

        if (ByteHelpers.StartsWithIgnoreAsciiCase(source, offset, Href))
        {
            afterName = offset + Href.Length;
            return true;
        }

        afterName = -1;
        return false;
    }

    /// <summary>Scans an absolute http(s) URL bounded by <paramref name="quote"/>, whitespace, or <c>&gt;</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="urlStart">First candidate byte of the URL.</param>
    /// <param name="quote">Closing quote byte.</param>
    /// <param name="urlEnd">Offset just past the last URL byte on success.</param>
    /// <returns>True when the URL is well-formed and ends with <paramref name="quote"/>.</returns>
    private static bool TryScanUrl(ReadOnlySpan<byte> source, int urlStart, byte quote, out int urlEnd)
    {
        urlEnd = -1;
        if (!ByteHelpers.StartsWithIgnoreAsciiCase(source, urlStart, HttpScheme)
            && !ByteHelpers.StartsWithIgnoreAsciiCase(source, urlStart, HttpsScheme))
        {
            return false;
        }

        var rel = source[urlStart..].IndexOfAny(UrlTerminators);
        if (rel < 0)
        {
            return false;
        }

        var stopAt = urlStart + rel;
        if (source[stopAt] != quote)
        {
            return false;
        }

        urlEnd = stopAt;
        return true;
    }
}
