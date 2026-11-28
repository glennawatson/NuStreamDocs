// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Playgrounds;

/// <summary>Parses UTF-8 <c>http-request</c> fence payloads and decodes HTML entities when invoked from post-render HTML.</summary>
internal static class HttpRequestParser
{
    /// <summary>Length of <c>&amp;lt;</c> and <c>&amp;gt;</c> HTML entities.</summary>
    private const int ShortAngleEntityLength = 4;

    /// <summary>Length of <c>&amp;amp;</c> HTML entity.</summary>
    private const int AmpEntityLength = 5;

    /// <summary>Length of <c>&amp;quot;</c> and <c>&amp;#x27;</c> HTML entities.</summary>
    private const int QuoteEntityLength = 6;

    /// <summary>ASCII lowercase-to-uppercase offset.</summary>
    private const int AsciiUpperOffset = 'a' - 'A';

    /// <summary>Parses <paramref name="source"/> into an <see cref="HttpRequestSpec"/>.</summary>
    /// <param name="source">Decoded UTF-8 fence body.</param>
    /// <returns>Parsed HTTP request specification.</returns>
    internal static HttpRequestSpec Parse(ReadOnlySpan<byte> source)
    {
        var trimmed = source.Trim(" \t\r\n"u8);
        if (trimmed.IsEmpty)
        {
            return new([.. "GET"u8], [.. "/"u8], [], []);
        }

        var cursor = 0;
        var firstLine = ReadLine(trimmed, ref cursor);
        ParseRequestLine(firstLine, out var method, out var url);

        List<(byte[] Name, byte[] Value)> headers = [];
        while (cursor < trimmed.Length)
        {
            var line = ReadLine(trimmed, ref cursor);
            if (line.IsEmpty)
            {
                break;
            }

            var colon = line.IndexOf((byte)':');
            if (colon <= 0)
            {
                continue;
            }

            var headerName = line[..colon].Trim(" \t"u8).ToArray();
            var headerVal = line[(colon + 1)..].Trim(" \t"u8).ToArray();
            headers.Add((headerName, headerVal));
        }

        var body = cursor < trimmed.Length ? trimmed[cursor..].Trim(" \t\r\n"u8).ToArray() : [];
        return new(method, url, [.. headers], body);
    }

    /// <summary>Decodes basic HTML entities (<c>&amp;lt;</c>, <c>&amp;gt;</c>, <c>&amp;quot;</c>, <c>&amp;#39;</c>, <c>&amp;amp;</c>) emitted inside <c>&lt;code&gt;</c> blocks.</summary>
    /// <param name="encoded">HTML-escaped UTF-8 bytes.</param>
    /// <returns>Decoded UTF-8 bytes.</returns>
    internal static byte[] DecodeHtmlEntities(ReadOnlySpan<byte> encoded)
    {
        if (encoded.IndexOf((byte)'&') < 0)
        {
            return encoded.ToArray();
        }

        ArrayBufferWriter<byte> writer = new(encoded.Length);
        var idx = 0;
        while (idx < encoded.Length)
        {
            if (encoded[idx] == (byte)'&' && TryMatchEntity(encoded[idx..], out var replacement, out var consumed))
            {
                var dst = writer.GetSpan(1);
                dst[0] = replacement;
                writer.Advance(1);
                idx += consumed;
                continue;
            }

            var plain = writer.GetSpan(1);
            plain[0] = encoded[idx];
            writer.Advance(1);
            idx++;
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Matches one standard HTML character entity at the start of <paramref name="slice"/>.</summary>
    /// <param name="slice">Remaining UTF-8 bytes starting with <c>&amp;</c>.</param>
    /// <param name="decoded">Decoded ASCII byte.</param>
    /// <param name="consumed">Entity byte length.</param>
    /// <returns><c>true</c> when recognized.</returns>
    private static bool TryMatchEntity(ReadOnlySpan<byte> slice, out byte decoded, out int consumed)
    {
        if (slice.StartsWith("&lt;"u8))
        {
            decoded = (byte)'<';
            consumed = ShortAngleEntityLength;
            return true;
        }

        if (slice.StartsWith("&gt;"u8))
        {
            decoded = (byte)'>';
            consumed = ShortAngleEntityLength;
            return true;
        }

        if (slice.StartsWith("&amp;"u8))
        {
            decoded = (byte)'&';
            consumed = AmpEntityLength;
            return true;
        }

        if (slice.StartsWith("&quot;"u8))
        {
            decoded = (byte)'"';
            consumed = QuoteEntityLength;
            return true;
        }

        if (slice.StartsWith("&#39;"u8))
        {
            decoded = (byte)'\'';
            consumed = AmpEntityLength;
            return true;
        }

        decoded = 0;
        consumed = 0;
        return false;
    }

    /// <summary>Splits <c>METHOD URL</c> (defaulting method to <c>GET</c> when only a URL is given).</summary>
    /// <param name="line">First trimmed line.</param>
    /// <param name="method">Uppercase method bytes.</param>
    /// <param name="url">Target URL bytes.</param>
    private static void ParseRequestLine(ReadOnlySpan<byte> line, out byte[] method, out byte[] url)
    {
        var space = line.IndexOfAny((byte)' ', (byte)'\t');
        if (space < 0)
        {
            method = [.. "GET"u8];
            url = line.ToArray();
            return;
        }

        var rawMethod = line[..space];
        method = new byte[rawMethod.Length];
        for (var i = 0; i < rawMethod.Length; i++)
        {
            var b = rawMethod[i];
            method[i] = b is >= (byte)'a' and <= (byte)'z' ? (byte)(b - AsciiUpperOffset) : b;
        }

        url = line[(space + 1)..].Trim(" \t"u8).ToArray();
    }

    /// <summary>Reads one LF/CRLF line from <paramref name="source"/> and trims trailing CR/whitespace.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Current offset.</param>
    /// <returns>Trimmed line slice.</returns>
    private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> source, ref int cursor)
    {
        var nl = source[cursor..].IndexOf((byte)'\n');
        if (nl < 0)
        {
            var rest = source[cursor..].Trim(" \t\r"u8);
            cursor = source.Length;
            return rest;
        }

        var line = source.Slice(cursor, nl).Trim(" \t\r"u8);
        cursor += nl + 1;
        return line;
    }
}
