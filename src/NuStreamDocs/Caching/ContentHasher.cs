// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Hashing;

namespace NuStreamDocs.Caching;

/// <summary>
/// xxHash3-based content hashing helper.
/// </summary>
/// <remarks>
/// xxHash3 is a 64-bit, vector-friendly, non-cryptographic hash; the
/// allocation profile and throughput beat SHA / MD5 by an order of
/// magnitude and "is this file the same as last build?" doesn't need
/// cryptographic strength.
/// </remarks>
public static class ContentHasher
{
    /// <summary>Length in bytes of an xxHash3 digest.</summary>
    private const int HashByteLength = 8;

    /// <summary>Length of the hex-encoded digest (two chars per byte).</summary>
    private const int HashHexLength = HashByteLength * 2;

    /// <summary>Hashes <paramref name="utf8"/> and returns the lowercase hex digest.</summary>
    /// <param name="utf8">UTF-8 source bytes.</param>
    /// <returns>16-character lowercase hex string.</returns>
    public static string HashHex(ReadOnlySpan<byte> utf8)
    {
        Span<byte> digest = stackalloc byte[HashByteLength];
        XxHash3.Hash(utf8, digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>Hashes the contents of a file and returns the lowercase hex digest.</summary>
    /// <param name="path">Absolute path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hex digest.</returns>
    public static async ValueTask<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        await using var stream = File.OpenRead(path);
        var hasher = new XxHash3();

        // 64 KiB read window; bounded so a multi-MB page doesn't spike
        // working set on the worker thread.
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                hasher.Append(buffer.AsSpan(0, read));
            }

            Span<byte> digest = stackalloc byte[HashByteLength];
            hasher.GetCurrentHash(digest);
            return Convert.ToHexStringLower(digest);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Returns the canonical empty-content digest.</summary>
    /// <returns>Hex digest of zero-length input.</returns>
    public static string EmptyHex() => new('0', HashHexLength);
}
