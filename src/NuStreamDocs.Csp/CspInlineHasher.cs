// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;

namespace NuStreamDocs.Csp;

/// <summary>Hashes inline <c>&lt;script&gt;</c> / <c>&lt;style&gt;</c> bodies in a page into <c>'sha256-…'</c> CSP source tokens.</summary>
public static class CspInlineHasher
{
    /// <summary>Length of the <c>'sha256-</c> prefix.</summary>
    private const int Sha256TokenPrefixLength = 8;

    /// <summary>Returns true when <paramref name="html"/> may contain an inline script or style worth hashing.</summary>
    /// <param name="html">Page HTML.</param>
    /// <returns>True when the cheap pre-filter matches.</returns>
    public static bool MayHaveInlineBlocks(ReadOnlySpan<byte> html) =>
        html.IndexOf("<script"u8) >= 0 || html.IndexOf("<style"u8) >= 0;

    /// <summary>Hashes every non-empty inline <c>&lt;script&gt;</c> body in <paramref name="html"/>, appending each <c>'sha256-…'</c> token to <paramref name="hashes"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="hashes">Destination list.</param>
    public static void HashScripts(ReadOnlySpan<byte> html, List<byte[]> hashes) =>
        ScanBlocks(html, "<script"u8, "</script>"u8, hashes);

    /// <summary>Hashes every non-empty inline <c>&lt;style&gt;</c> body in <paramref name="html"/>, appending each <c>'sha256-…'</c> token to <paramref name="hashes"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="hashes">Destination list.</param>
    public static void HashStyles(ReadOnlySpan<byte> html, List<byte[]> hashes) =>
        ScanBlocks(html, "<style"u8, "</style>"u8, hashes);

    /// <summary>Walks <paramref name="html"/> for every <c>{open}…&gt;…{close}</c> block and appends the formatted hash of its body to <paramref name="sink"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="open">Opening-tag prefix.</param>
    /// <param name="close">Closing tag.</param>
    /// <param name="sink">Destination list.</param>
    private static void ScanBlocks(ReadOnlySpan<byte> html, ReadOnlySpan<byte> open, ReadOnlySpan<byte> close, List<byte[]> sink)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf(open);
            if (rel < 0)
            {
                break;
            }

            var tagOpenStart = cursor + rel;
            var afterOpen = html[(tagOpenStart + open.Length)..];
            var tagCloseRel = afterOpen.IndexOf((byte)'>');
            if (tagCloseRel < 0)
            {
                break;
            }

            var bodyStart = tagOpenStart + open.Length + tagCloseRel + 1;
            var endRel = html[bodyStart..].IndexOf(close);
            if (endRel < 0)
            {
                break;
            }

            var body = html.Slice(bodyStart, endRel);
            cursor = bodyStart + endRel + close.Length;
            if (body.IsEmpty)
            {
                continue;
            }

            SHA256.HashData(body, hash);
            sink.Add(FormatSha256Token(hash));
        }
    }

    /// <summary>Formats a hash as the bytes of <c>'sha256-&lt;base64&gt;'</c>.</summary>
    /// <param name="hash">The 32-byte SHA-256 digest.</param>
    /// <returns>The token bytes.</returns>
    private static byte[] FormatSha256Token(ReadOnlySpan<byte> hash)
    {
        Span<char> chars = stackalloc char[(SHA256.HashSizeInBytes + 2) / 3 * 4];
        Convert.TryToBase64Chars(hash, chars, out var charsWritten);
        var b64 = chars[..charsWritten];
        var buffer = new byte[Sha256TokenPrefixLength + b64.Length + 1];
        "'sha256-"u8.CopyTo(buffer);
        Encoding.UTF8.GetBytes(b64, buffer.AsSpan(Sha256TokenPrefixLength));
        buffer[^1] = (byte)'\'';
        return buffer;
    }
}
