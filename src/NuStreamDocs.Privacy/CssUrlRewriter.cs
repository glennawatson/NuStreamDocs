// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Stateless rewriter for CSS files: finds <c>url(...)</c> references
/// to absolute http(s) URLs, resolves relative URLs against the
/// stylesheet's own base, and registers + rewrites each one to its
/// local path through an <see cref="ExternalAssetRegistry"/>.
/// </summary>
/// <remarks>
/// Closes the Google Fonts loop: a fetched <c>fonts.css</c> typically
/// references <c>https://fonts.gstatic.com/.../font.woff2</c> URLs
/// inside <c>url()</c> tokens, which the per-page HTML scan never
/// sees.
/// </remarks>
internal static class CssUrlRewriter
{
    /// <summary>Length of the literal <c>url(</c> prefix.</summary>
    private const int UrlPrefixLength = 4;

    /// <summary>Bytes that can begin a <c>url(</c> token (case-insensitive on the leading <c>u</c>).</summary>
    private static readonly SearchValues<byte> UrlStart = SearchValues.Create("uU"u8);

    /// <summary>Whitespace bytes allowed between <c>url(</c> and the value.</summary>
    private static readonly SearchValues<byte> CssWhitespace = SearchValues.Create(" \t\r\n"u8);

    /// <summary>Rewrites every <c>url(...)</c> reference in <paramref name="css"/> against <paramref name="cssBaseUri"/>.</summary>
    /// <param name="css">UTF-8 CSS bytes.</param>
    /// <param name="cssBaseUri">Absolute URL the CSS file was fetched from; relative <c>url()</c> values are resolved against this.</param>
    /// <param name="registry">URL registry; new entries are appended for every external URL seen.</param>
    /// <param name="filter">Host filter; URLs whose host fails the filter are left as-is.</param>
    /// <returns>The rewritten CSS bytes.</returns>
    public static byte[] Rewrite(byte[] css, Uri cssBaseUri, ExternalAssetRegistry registry, HostFilter filter)
    {
        ArgumentNullException.ThrowIfNull(css);
        ArgumentNullException.ThrowIfNull(cssBaseUri);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(filter);

        var sink = new ArrayBufferWriter<byte>(css.Length);
        var span = (ReadOnlySpan<byte>)css;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < span.Length)
        {
            var rel = span[cursor..].IndexOfAny(UrlStart);
            if (rel < 0)
            {
                break;
            }

            var tokenStart = cursor + rel;
            if (!TryReadUrlToken(span, tokenStart, out var quote, out var rawSlice, out var afterToken))
            {
                cursor = tokenStart + 1;
                continue;
            }

            var raw = Encoding.UTF8.GetString(rawSlice);
            if (!TryResolveAbsolute(raw, cssBaseUri, out var absolute) || !filter.ShouldLocalise(absolute))
            {
                cursor = afterToken;
                continue;
            }

            // Emit everything from lastEmit up to the start of this url() token, then the rewritten form.
            sink.Write(span[lastEmit..tokenStart]);
            var local = registry.GetOrAdd(absolute);
            sink.Write("url("u8);
            if (quote is not 0)
            {
                Span<byte> q = [quote];
                sink.Write(q);
            }

            sink.Write("/"u8);
            sink.Write(Encoding.UTF8.GetBytes(local));
            if (quote is not 0)
            {
                Span<byte> q = [quote];
                sink.Write(q);
            }

            sink.Write(")"u8);

            lastEmit = afterToken;
            cursor = afterToken;
        }

        if (lastEmit is 0)
        {
            return css;
        }

        sink.Write(span[lastEmit..]);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Tries to read a <c>url(...)</c> token starting at <paramref name="start"/> in <paramref name="css"/>.</summary>
    /// <param name="css">CSS bytes.</param>
    /// <param name="start">Position of the candidate <c>u</c> / <c>U</c>.</param>
    /// <param name="quote">Quote byte used by the URL value, or <c>0</c> for unquoted.</param>
    /// <param name="rawUrl">Span over the URL value (no quotes, no surrounding whitespace).</param>
    /// <param name="afterToken">Index just past the closing <c>)</c> on success.</param>
    /// <returns>True when a complete token was matched.</returns>
    private static bool TryReadUrlToken(ReadOnlySpan<byte> css, int start, out byte quote, out ReadOnlySpan<byte> rawUrl, out int afterToken)
    {
        quote = 0;
        rawUrl = default;
        afterToken = start;

        if (!HasUrlPrefix(css, start))
        {
            return false;
        }

        var pos = SkipWhitespace(css, start + UrlPrefixLength);
        if (pos >= css.Length)
        {
            return false;
        }

        if (!TryReadValue(css, pos, out quote, out rawUrl, out var posAfterValue))
        {
            return false;
        }

        var beforeCloseParen = SkipWhitespace(css, posAfterValue);
        if (beforeCloseParen >= css.Length || css[beforeCloseParen] is not (byte)')')
        {
            return false;
        }

        afterToken = beforeCloseParen + 1;
        return true;
    }

    /// <summary>Returns true when <paramref name="css"/> at <paramref name="start"/> begins with the case-insensitive <c>url(</c> prefix.</summary>
    /// <param name="css">CSS bytes.</param>
    /// <param name="start">Candidate offset.</param>
    /// <returns>True when the four-byte prefix matches.</returns>
    private static bool HasUrlPrefix(ReadOnlySpan<byte> css, int start) =>
        start + UrlPrefixLength <= css.Length
            && css[start..(start + UrlPrefixLength)] is
                [(byte)'u' or (byte)'U', (byte)'r' or (byte)'R', (byte)'l' or (byte)'L', (byte)'('];

    /// <summary>Advances past any CSS whitespace bytes from <paramref name="from"/>.</summary>
    /// <param name="css">CSS bytes.</param>
    /// <param name="from">Cursor.</param>
    /// <returns>The first non-whitespace position at or after <paramref name="from"/>.</returns>
    private static int SkipWhitespace(ReadOnlySpan<byte> css, int from)
    {
        var pos = from;
        while (pos < css.Length && CssWhitespace.Contains(css[pos]))
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Reads the URL body — optional quote, body bytes, optional matching quote — and reports the position past the body (and past the closing quote when present).</summary>
    /// <param name="css">CSS bytes.</param>
    /// <param name="start">Position at the start of the body (post-whitespace).</param>
    /// <param name="quote">Quote byte, or <c>0</c> when the body is unquoted.</param>
    /// <param name="rawUrl">Span over the URL bytes.</param>
    /// <param name="afterValue">Position immediately after the body (and after the closing quote when present).</param>
    /// <returns>True when a body was read.</returns>
    private static bool TryReadValue(ReadOnlySpan<byte> css, int start, out byte quote, out ReadOnlySpan<byte> rawUrl, out int afterValue)
    {
        quote = 0;
        rawUrl = default;
        afterValue = start;

        var maybeQuote = css[start];
        var bodyStart = maybeQuote is (byte)'"' or (byte)'\''
            ? start + 1
            : start;
        if (bodyStart != start)
        {
            quote = maybeQuote;
        }

        var bodyEndRel = quote is 0 ? FindUnquotedEnd(css, bodyStart) : css[bodyStart..].IndexOf(quote);
        if (bodyEndRel < 0)
        {
            return false;
        }

        var bodyEndAbs = quote is 0 ? bodyEndRel : bodyStart + bodyEndRel;
        rawUrl = css.Slice(bodyStart, bodyEndAbs - bodyStart);
        afterValue = quote is 0 ? bodyEndAbs : bodyEndAbs + 1;
        return true;
    }

    /// <summary>Returns the absolute index of the closing-paren / quote / whitespace that terminates an unquoted <c>url(...)</c> body.</summary>
    /// <param name="css">CSS bytes.</param>
    /// <param name="from">Body start.</param>
    /// <returns>Absolute index of the terminator, or -1 when none found.</returns>
    private static int FindUnquotedEnd(ReadOnlySpan<byte> css, int from)
    {
        for (var i = from; i < css.Length; i++)
        {
            var b = css[i];
            if (b is (byte)')' or (byte)'"' or (byte)'\'' || CssWhitespace.Contains(b))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Resolves <paramref name="raw"/> against <paramref name="baseUri"/> when it's relative, or accepts it as-is when it's already an absolute http(s) URL.</summary>
    /// <param name="raw">Raw URL text from the <c>url()</c> token.</param>
    /// <param name="baseUri">Absolute URL the CSS file was fetched from.</param>
    /// <param name="absolute">Resolved absolute URL on success.</param>
    /// <returns>True when <paramref name="absolute"/> was set.</returns>
    private static bool TryResolveAbsolute(string raw, Uri baseUri, out string absolute)
    {
        absolute = string.Empty;
        if (string.IsNullOrEmpty(raw) || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var asAbsolute))
        {
            if (asAbsolute.Scheme is not ("http" or "https"))
            {
                return false;
            }

            absolute = asAbsolute.AbsoluteUri;
            return true;
        }

        if (!Uri.TryCreate(baseUri, raw, out var resolved) || resolved.Scheme is not ("http" or "https"))
        {
            return false;
        }

        absolute = resolved.AbsoluteUri;
        return true;
    }
}
