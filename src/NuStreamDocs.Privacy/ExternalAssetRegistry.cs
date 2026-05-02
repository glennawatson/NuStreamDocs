// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Thread-safe registry that maps external URLs to their target local
/// paths under the configured asset directory.
/// </summary>
/// <remarks>
/// Per-page <see cref="PrivacyPlugin"/> hooks register URLs as they
/// scan rendered HTML; the finalize pass enumerates the registry to
/// download every unique URL once. Storage is byte-array keyed (with
/// <see cref="ByteArrayComparer"/>) so hot-path scanners never UTF-16
/// encode the URL just to register it; <see cref="string"/> shapes are
/// adapted at the boundary via <see cref="Utf8StackBuffer"/>.
/// </remarks>
internal sealed class ExternalAssetRegistry
{
    /// <summary>Length in bytes of the xxHash3 digest used to derive filenames.</summary>
    private const int HashByteLength = 8;

    /// <summary>Hex chars per byte — emits one nibble pair per source byte.</summary>
    private const int HexCharsPerByte = 2;

    /// <summary>Bit-shift count to extract the high nibble of a byte.</summary>
    private const int HighNibbleShift = 4;

    /// <summary>Bit mask for the low nibble of a byte.</summary>
    private const byte LowNibbleMask = 0x0F;

    /// <summary>Pre-encoded asset directory bytes (no trailing slash).</summary>
    private readonly byte[] _assetDirectoryBytes;

    /// <summary>Concurrent URL-bytes → local-relative-path-bytes map.</summary>
    private readonly ConcurrentDictionary<byte[], byte[]> _urlToLocal = new(ByteArrayComparer.Instance);

    /// <summary>Initializes a new instance of the <see cref="ExternalAssetRegistry"/> class.</summary>
    /// <param name="assetDirectory">Forward-slash relative directory to write under (e.g. <c>assets/external</c>).</param>
    public ExternalAssetRegistry(string assetDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetDirectory);
        _assetDirectoryBytes = Encoding.UTF8.GetBytes(assetDirectory.TrimEnd('/'));
    }

    /// <summary>Returns the local path bytes for <paramref name="urlBytes"/>, registering a new entry on first sight.</summary>
    /// <param name="urlBytes">External URL bytes.</param>
    /// <returns>Forward-slash relative path bytes under the output root (e.g. <c>assets/external/3a7f0d.png</c>).</returns>
    public byte[] GetOrAdd(ReadOnlySpan<byte> urlBytes)
    {
        if (urlBytes.IsEmpty)
        {
            throw new ArgumentException("URL bytes must not be empty.", nameof(urlBytes));
        }

        // Look up using a temporary copy to honor the ByteArrayComparer contract; the factory
        // only fires on first sight so the local-path alloc only pays for registered URLs.
        var key = urlBytes.ToArray();
        return _urlToLocal.GetOrAdd(key, static (k, dir) => BuildLocalPathBytes(k, dir), _assetDirectoryBytes);
    }

    /// <summary>String adapter — encodes <paramref name="url"/> into a stack-or-pool buffer and delegates to the byte overload.</summary>
    /// <param name="url">External URL string.</param>
    /// <returns>Forward-slash relative path string.</returns>
    public string GetOrAdd(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        using var buf = new Utf8StackBuffer(url, stackalloc byte[Utf8StackBuffer.StackSize]);
        var localBytes = GetOrAdd(buf.Bytes);
        return Encoding.UTF8.GetString(localBytes);
    }

    /// <summary>Gets a snapshot of the registered <c>(url, localPath)</c> pairs as strings — the boundary the downloader and CSP hash collector cross.</summary>
    /// <returns>Right-sized snapshot array.</returns>
    public (string Url, string LocalPath)[] EntriesSnapshot()
    {
        KeyValuePair<byte[], byte[]>[] snapshot = [.. _urlToLocal];
        var result = new (string Url, string LocalPath)[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            result[i] = (Encoding.UTF8.GetString(snapshot[i].Key), Encoding.UTF8.GetString(snapshot[i].Value));
        }

        return result;
    }

    /// <summary>Gets a snapshot of the registered URLs as strings.</summary>
    /// <returns>Right-sized URL array.</returns>
    public string[] UrlsSnapshot()
    {
        var entries = EntriesSnapshot();
        var urls = new string[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            urls[i] = entries[i].Url;
        }

        return urls;
    }

    /// <summary>Computes the local-path bytes for <paramref name="urlBytes"/> from its xxHash3 digest and original extension.</summary>
    /// <param name="urlBytes">External URL bytes.</param>
    /// <param name="assetDirectoryBytes">Forward-slash relative directory bytes (no trailing slash).</param>
    /// <returns>Forward-slash relative path bytes.</returns>
    private static byte[] BuildLocalPathBytes(byte[] urlBytes, byte[] assetDirectoryBytes)
    {
        Span<byte> digest = stackalloc byte[HashByteLength];
        XxHash3.Hash(urlBytes, digest);
        Span<byte> hexBuf = stackalloc byte[HashByteLength * 2];
        WriteLowerHex(digest, hexBuf);
        var extBytes = ExtractExtensionBytes(urlBytes);

        var totalLen = assetDirectoryBytes.Length + 1 + hexBuf.Length + extBytes.Length;
        var output = new byte[totalLen];
        var write = 0;
        assetDirectoryBytes.CopyTo(output, write);
        write += assetDirectoryBytes.Length;
        output[write++] = (byte)'/';
        hexBuf.CopyTo(output.AsSpan(write));
        write += hexBuf.Length;
        if (extBytes.Length > 0)
        {
            extBytes.CopyTo(output.AsSpan(write));
        }

        return output;
    }

    /// <summary>Writes <paramref name="digest"/> as lowercase ASCII hex into <paramref name="dst"/>.</summary>
    /// <param name="digest">Digest bytes.</param>
    /// <param name="dst">Destination span (length must be 2× digest length).</param>
    private static void WriteLowerHex(ReadOnlySpan<byte> digest, Span<byte> dst)
    {
        const string LowerHex = "0123456789abcdef";
        for (var i = 0; i < digest.Length; i++)
        {
            dst[(i * HexCharsPerByte) + 0] = (byte)LowerHex[digest[i] >> HighNibbleShift];
            dst[(i * HexCharsPerByte) + 1] = (byte)LowerHex[digest[i] & LowNibbleMask];
        }
    }

    /// <summary>Pulls the file extension (including the leading dot) off the URL's path component.</summary>
    /// <param name="urlBytes">External URL bytes.</param>
    /// <returns>Extension bytes (with leading dot) or an empty span when none is recognizable.</returns>
    private static ReadOnlySpan<byte> ExtractExtensionBytes(ReadOnlySpan<byte> urlBytes)
    {
        // Locate the path: skip past "scheme://", then the host, until the first '/'.
        var schemeEnd = urlBytes.IndexOf("://"u8);
        if (schemeEnd < 0)
        {
            return default;
        }

        var afterScheme = schemeEnd + 3;
        var pathStart = afterScheme + urlBytes[afterScheme..].IndexOf((byte)'/');
        if (pathStart <= afterScheme)
        {
            return default;
        }

        var path = urlBytes[pathStart..];
        var queryIdx = path.IndexOf((byte)'?');
        if (queryIdx >= 0)
        {
            path = path[..queryIdx];
        }

        var hashIdx = path.IndexOf((byte)'#');
        if (hashIdx >= 0)
        {
            path = path[..hashIdx];
        }

        var dot = path.LastIndexOf((byte)'.');
        if (dot < 0 || dot >= path.Length - 1)
        {
            return default;
        }

        var ext = path[dot..];
        if (ext.Length > 8)
        {
            return default;
        }

        for (var i = 1; i < ext.Length; i++)
        {
            var c = ext[i];
            if (!IsAsciiLetterOrDigit(c))
            {
                return default;
            }
        }

        return ext;
    }

    /// <summary>Byte-level ASCII letter/digit test.</summary>
    /// <param name="b">Byte to classify.</param>
    /// <returns>True for <c>[A-Za-z0-9]</c>.</returns>
    private static bool IsAsciiLetterOrDigit(byte b) =>
        b is (>= (byte)'A' and <= (byte)'Z')
            or (>= (byte)'a' and <= (byte)'z')
            or (>= (byte)'0' and <= (byte)'9');
}
