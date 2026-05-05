// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Hashing;
using NuStreamDocs.Common;

namespace NuStreamDocs.Caching;

/// <summary>
/// xxHash3-based content hashing helper.
/// </summary>
/// <remarks>
/// Wraps <see cref="XxHash3"/> from <c>System.IO.Hashing</c> — Microsoft's vector-friendly,
/// 64-bit non-cryptographic hash. Digests are returned as raw 8-byte arrays (no hex / base64
/// encoding) so consumers stay byte-shaped end-to-end; persistence layers re-encode at the
/// boundary (e.g. <c>Utf8JsonWriter.WriteBase64StringValue</c>).
/// </remarks>
public static class ContentHasher
{
    /// <summary>xxHash3 digest size in bytes; matches <c>System.IO.Hashing.XxHash3.HashLengthInBytes</c>.</summary>
    private const int Xxhash3DigestBytes = 8;

    /// <summary>Gets the length in bytes of an xxHash3 digest.</summary>
    public static int HashByteLength => Xxhash3DigestBytes;

    /// <summary>Hashes <paramref name="utf8"/> and returns the raw 8-byte digest.</summary>
    /// <param name="utf8">UTF-8 source bytes.</param>
    /// <returns>The 8-byte xxHash3 digest.</returns>
    public static byte[] Hash(ReadOnlySpan<byte> utf8)
    {
        var digest = new byte[HashByteLength];
        XxHash3.Hash(utf8, digest);
        return digest;
    }

    /// <summary>Hashes the contents of a file and returns the raw 8-byte digest.</summary>
    /// <param name="path">Absolute path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The 8-byte xxHash3 digest.</returns>
    public static async ValueTask<byte[]> HashFileAsync(FilePath path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path.Value);

        await using var stream = File.OpenRead(path.Value);
        XxHash3 hasher = new();

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

            var digest = new byte[HashByteLength];
            hasher.GetCurrentHash(digest);
            return digest;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Returns the canonical empty-content digest.</summary>
    /// <returns>The xxHash3 digest of zero-length input — an 8-byte zero-filled array.</returns>
    public static byte[] Empty() => new byte[HashByteLength];
}
