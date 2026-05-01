// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Collections.Frozen;
using System.IO.Compression;
using System.Reflection;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>
/// Loads the embedded Material Design Icons bundle into a frozen
/// lookup at first access.
/// </summary>
/// <remarks>
/// On-disk layout of the embedded resource <c>mdi-icons.bin</c>:
/// <list type="number">
/// <item><c>uint32</c> — entry count (little-endian).</item>
/// <item>Per entry: <c>uint16</c> name length, the UTF-8 name bytes, <c>uint32</c> SVG length, the UTF-8 SVG bytes.</item>
/// </list>
/// The whole file is wrapped in a deflate stream so the on-NuGet
/// payload stays small. <see cref="MdiIconBundle"/> wraps that into an
/// <see cref="MdiIconLookup"/> at first access; subsequent calls are
/// constant-time.
/// </remarks>
public static class MdiIconBundle
{
    /// <summary>Resource name of the embedded MDI bundle.</summary>
    private const string ResourceName = "NuStreamDocs.Icons.MaterialDesign.mdi-icons.bin";

    /// <summary>Lazy initialiser — the bundle is decoded once on first access.</summary>
    private static readonly Lazy<MdiIconLookup> LazyDefault = new(LoadDefault, isThreadSafe: true);

    /// <summary>Gets the default MDI lookup loaded from the embedded bundle.</summary>
    /// <remarks>
    /// When no bundle is shipped (i.e. the regenerator hasn't been run yet) this
    /// resolves to an empty lookup — calls fall through to the font-ligature
    /// fallback, matching the pre-MDI behaviour. Run the regen tool to populate.
    /// </remarks>
    public static MdiIconLookup Default => LazyDefault.Value;

    /// <summary>Loads + decompresses the embedded bundle, or returns an empty lookup when no bundle is present.</summary>
    /// <returns>Resolved icon lookup.</returns>
    private static MdiIconLookup LoadDefault()
    {
        var assembly = typeof(MdiIconBundle).Assembly;
        using var raw = assembly.GetManifestResourceStream(ResourceName);
        if (raw is null)
        {
            return new(
                blob: [],
                index: new Dictionary<byte[], (int Offset, int Length)>(ByteArrayKeyComparer.Instance)
                    .ToFrozenDictionary(ByteArrayKeyComparer.Instance));
        }

        using var deflate = new DeflateStream(raw, CompressionMode.Decompress);
        using var memory = new MemoryStream();
        deflate.CopyTo(memory);
        return Decode(memory.ToArray());
    }

    /// <summary>Decodes the in-memory bundle bytes into a lookup.</summary>
    /// <param name="bytes">Decompressed bundle.</param>
    /// <returns>Resolved lookup.</returns>
    private static MdiIconLookup Decode(byte[] bytes)
    {
        var span = bytes.AsSpan();
        var entryCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(span);
        var cursor = sizeof(uint);

        var blobBuffer = new List<byte>(bytes.Length);
        var entries = new Dictionary<byte[], (int Offset, int Length)>(entryCount, ByteArrayKeyComparer.Instance);
        for (var i = 0; i < entryCount; i++)
        {
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(span[cursor..]);
            cursor += sizeof(ushort);
            var nameKey = span.Slice(cursor, nameLength).ToArray();
            cursor += nameLength;

            var svgLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(span[cursor..]);
            cursor += sizeof(uint);
            var blobOffset = blobBuffer.Count;
            for (var j = 0; j < svgLength; j++)
            {
                blobBuffer.Add(span[cursor + j]);
            }

            cursor += svgLength;
            entries[nameKey] = (blobOffset, svgLength);
        }

        return new([.. blobBuffer], entries.ToFrozenDictionary(ByteArrayKeyComparer.Instance));
    }
}
