// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;

namespace NuStreamDocs.Common;

/// <summary>Builds the <c>'sha256-&lt;base64&gt;'</c> CSP source token for an inline script or style body.</summary>
public static class CspSourceToken
{
    /// <summary>Length of the <c>'sha256-</c> prefix.</summary>
    private const int Sha256TokenPrefixLength = 8;

    /// <summary>Hashes <paramref name="body"/> with SHA-256 and returns the bytes of its <c>'sha256-…'</c> CSP source token.</summary>
    /// <param name="body">Inline block body bytes.</param>
    /// <returns>The token bytes.</returns>
    public static byte[] FromBody(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(body, hash);
        return FromHash(hash);
    }

    /// <summary>Returns the bytes of the <c>'sha256-&lt;base64&gt;'</c> token for an already-computed digest.</summary>
    /// <param name="hash">The 32-byte SHA-256 digest.</param>
    /// <returns>The token bytes.</returns>
    public static byte[] FromHash(ReadOnlySpan<byte> hash)
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
