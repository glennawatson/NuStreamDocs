// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Byte-level scanner for CSS <c>url(...)</c> tokens. Supports
/// double-quoted, single-quoted, and unquoted forms.
/// </summary>
internal static class CssUrlBytes
{
    /// <summary>Bytes that may start a <c>url</c> token (case-insensitive).</summary>
    private static readonly SearchValues<byte> TokenStart = SearchValues.Create("uU"u8);

    /// <summary>Bytes that terminate an unquoted URL value.</summary>
    private static readonly SearchValues<byte> UnquotedTerminators = SearchValues.Create(" \t\r\n)\"'"u8);

    /// <summary>Gets the lowercase <c>url(</c> opener.</summary>
    private static ReadOnlySpan<byte> UrlOpen => "url("u8;

    /// <summary>Gets the lowercase <c>http://</c> scheme.</summary>
    private static ReadOnlySpan<byte> HttpScheme => "http://"u8;

    /// <summary>Gets the lowercase <c>https://</c> scheme.</summary>
    private static ReadOnlySpan<byte> HttpsScheme => "https://"u8;

    /// <summary>Walks <paramref name="source"/>, copying through verbatim, but rewriting every <c>url(...)</c> token whose URL the registry localizes.</summary>
    /// <param name="source">UTF-8 source span.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink the rewritten output lands in.</param>
    /// <returns>True when at least one URL was rewritten.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> source, in UrlRewriteContext ctx, IBufferWriter<byte> sink) =>
        UrlScanLoop.Run(source, TokenStart, sink, ctx, TryRewriteAt);

    /// <summary>Walks <paramref name="source"/> in audit mode, recording every URL the host filter accepts.</summary>
    /// <param name="source">UTF-8 source span.</param>
    /// <param name="audit">Audit collector.</param>
    public static void AuditInto(ReadOnlySpan<byte> source, UrlAuditContext audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        UrlScanLoop.RunAudit(source, TokenStart, audit, TryAuditAt);
    }

    /// <summary>Tries to rewrite a <c>url(...)</c> token at <paramref name="p"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when the token was rewritten.</returns>
    private static bool TryRewriteAt(ReadOnlySpan<byte> source, int p, in UrlRewriteContext ctx, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!TryMatchToken(source, p, out var tokenEnd, out var urlStart, out var urlEnd, out var quote))
        {
            advanceTo = p + 1;
            return false;
        }

        var urlBytes = source[urlStart..urlEnd];
        if (!ctx.Filter.ShouldLocalize(urlBytes))
        {
            advanceTo = tokenEnd;
            return false;
        }

        var localBytes = ctx.Registry.GetOrAdd(urlBytes);
        sink.Write(source[lastEmit..p]);
        WriteRewrittenToken(quote, localBytes, sink);
        lastEmit = tokenEnd;
        advanceTo = tokenEnd;
        return true;
    }

    /// <summary>Audit-mode counterpart of <see cref="TryRewriteAt"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="audit">Audit collector.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when a URL was recorded.</returns>
    private static bool TryAuditAt(ReadOnlySpan<byte> source, int p, UrlAuditContext audit, out int advanceTo)
    {
        if (!TryMatchToken(source, p, out var tokenEnd, out var urlStart, out var urlEnd, out _))
        {
            advanceTo = p + 1;
            return false;
        }

        var urlBytes = source[urlStart..urlEnd];
        if (audit.Filter.ShouldLocalize(urlBytes))
        {
            audit.Set.TryAdd([.. urlBytes], 0);
        }

        advanceTo = tokenEnd;
        return true;
    }

    /// <summary>Validates a <c>url(...)</c> token at <paramref name="p"/>; returns the URL byte range and the optional quote byte (0 when unquoted).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="tokenEnd">Offset just past the closing <c>)</c> on success.</param>
    /// <param name="urlStart">First URL byte on success.</param>
    /// <param name="urlEnd">Offset just past the URL on success.</param>
    /// <param name="quote">Quote byte (or 0 for unquoted forms).</param>
    /// <returns>True on a successful match.</returns>
    private static bool TryMatchToken(ReadOnlySpan<byte> source, int p, out int tokenEnd, out int urlStart, out int urlEnd, out byte quote)
    {
        tokenEnd = -1;
        urlStart = -1;
        urlEnd = -1;
        quote = 0;
        if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(source, p, UrlOpen))
        {
            return false;
        }

        var afterOpen = AsciiByteHelpers.SkipWhitespace(source, p + UrlOpen.Length);
        if (afterOpen >= source.Length)
        {
            return false;
        }

        var maybeQuote = source[afterOpen];
        if (maybeQuote is (byte)'"' or (byte)'\'')
        {
            quote = maybeQuote;
            urlStart = afterOpen + 1;
        }
        else
        {
            urlStart = afterOpen;
        }

        return TryScanUrlAndClose(source, urlStart, quote, out urlEnd, out tokenEnd);
    }

    /// <summary>Scans the URL bytes after the optional opening quote and validates the closing <c>)</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="urlStart">First URL byte offset.</param>
    /// <param name="quote">Quote byte, or 0 for unquoted.</param>
    /// <param name="urlEnd">Offset just past the URL on success.</param>
    /// <param name="tokenEnd">Offset just past the closing <c>)</c> on success.</param>
    /// <returns>True when the URL is well-formed and the token closes properly.</returns>
    private static bool TryScanUrlAndClose(ReadOnlySpan<byte> source, int urlStart, byte quote, out int urlEnd, out int tokenEnd)
    {
        urlEnd = -1;
        tokenEnd = -1;
        if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(source, urlStart, HttpScheme)
            && !AsciiByteHelpers.StartsWithIgnoreAsciiCase(source, urlStart, HttpsScheme))
        {
            return false;
        }

        urlEnd = quote is 0
            ? FindUnquotedEnd(source, urlStart)
            : FindQuotedEnd(source, urlStart, quote);
        if (urlEnd < 0)
        {
            return false;
        }

        var p = urlEnd;
        if (quote is not 0)
        {
            p++;
        }

        p = AsciiByteHelpers.SkipWhitespace(source, p);
        if (p >= source.Length || source[p] is not (byte)')')
        {
            return false;
        }

        tokenEnd = p + 1;
        return true;
    }

    /// <summary>Finds the end offset of an unquoted URL (first whitespace, paren, or quote byte).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="urlStart">URL start offset.</param>
    /// <returns>Offset just past the URL, or <c>-1</c> when unterminated.</returns>
    private static int FindUnquotedEnd(ReadOnlySpan<byte> source, int urlStart)
    {
        var rel = source[urlStart..].IndexOfAny(UnquotedTerminators);
        return rel < 0 ? -1 : urlStart + rel;
    }

    /// <summary>Finds the closing quote of a quoted URL.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="urlStart">URL start offset.</param>
    /// <param name="quote">Quote byte.</param>
    /// <returns>Offset of the closing quote, or <c>-1</c> when unterminated.</returns>
    private static int FindQuotedEnd(ReadOnlySpan<byte> source, int urlStart, byte quote)
    {
        var rel = source[urlStart..].IndexOf(quote);
        return rel < 0 ? -1 : urlStart + rel;
    }

    /// <summary>Writes the rewritten <c>url({q}/{local}{q})</c> token into the sink.</summary>
    /// <param name="quote">Quote byte (0 for unquoted).</param>
    /// <param name="local">Local rewrite path.</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void WriteRewrittenToken(byte quote, ReadOnlySpan<byte> local, IBufferWriter<byte> sink)
    {
        sink.Write(UrlOpen);
        if (quote is not 0)
        {
            var q = sink.GetSpan(1);
            q[0] = quote;
            sink.Advance(1);
        }

        var slash = sink.GetSpan(1);
        slash[0] = (byte)'/';
        sink.Advance(1);
        sink.Write(local);
        if (quote is not 0)
        {
            var q = sink.GetSpan(1);
            q[0] = quote;
            sink.Advance(1);
        }

        var close = sink.GetSpan(1);
        close[0] = (byte)')';
        sink.Advance(1);
    }
}
