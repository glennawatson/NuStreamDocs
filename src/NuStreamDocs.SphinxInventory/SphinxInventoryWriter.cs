// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Compression;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.SphinxInventory;

/// <summary>
/// Stateless writer that emits a Sphinx v2 <c>objects.inv</c> inventory
/// for an autorefs snapshot. Header is plain UTF-8; body is one
/// <c>NAME std:label -1 URI -</c> line per entry, zlib-compressed.
/// All bytes are written through <see cref="IBufferWriter{T}"/> so the
/// only string allocation on the hot path is the per-entry UTF-8
/// encode of entry values supplied by the
/// registry snapshot — those are <see cref="string"/>s already, so
/// the encode is unavoidable; everything else (delimiters, fixed
/// fields, header) is byte-literal.
/// </summary>
internal static class SphinxInventoryWriter
{
    /// <summary>Gets header line 1 — Sphinx version marker (must be byte-exact).</summary>
    private static ReadOnlySpan<byte> HeaderLine1 => "# Sphinx inventory version 2\n"u8;

    /// <summary>Gets header prefix for the project line.</summary>
    private static ReadOnlySpan<byte> HeaderProjectPrefix => "# Project: "u8;

    /// <summary>Gets header prefix for the version line.</summary>
    private static ReadOnlySpan<byte> HeaderVersionPrefix => "# Version: "u8;

    /// <summary>Gets header line 4 — fixed string Sphinx tooling looks for verbatim.</summary>
    private static ReadOnlySpan<byte> HeaderCompressNote => "# The remainder of this file is compressed using zlib.\n"u8;

    /// <summary>Gets the per-entry middle field — domain (<c>std</c>), role (<c>label</c>), priority (<c>-1</c>).</summary>
    /// <remarks>
    /// Sphinx requires each entry as <c>NAME DOMAIN:ROLE PRIORITY URI DISPNAME</c>.
    /// We classify every uid as the generic <c>std:label</c> domain/role pair —
    /// it's what Sphinx's <c>any</c> role matches against and is the safest fit
    /// for arbitrary anchors. Priority <c>-1</c> defers to Sphinx's heuristics.
    /// </remarks>
    private static ReadOnlySpan<byte> EntryMiddle => " std:label -1 "u8;

    /// <summary>Gets the per-entry trailer — <c>"-"</c> (DISPNAME = use NAME) plus newline.</summary>
    private static ReadOnlySpan<byte> EntryTrailer => " -\n"u8;

    /// <summary>Writes the inventory file at <paramref name="outputPath"/>.</summary>
    /// <param name="outputPath">Absolute output path.</param>
    /// <param name="options">Header options (project, version, file name).</param>
    /// <param name="entries">Snapshot from the autorefs registry — <c>(uid, href)</c> pairs.</param>
    public static void Write(FilePath outputPath, SphinxInventoryOptions options, (byte[] Id, byte[] Url)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var stream = File.Create(outputPath);
        WriteHeader(stream, options);
        WriteCompressedBody(stream, entries);
    }

    /// <summary>Writes the four header lines plus the trailing newline.</summary>
    /// <param name="stream">Output stream.</param>
    /// <param name="options">Header options.</param>
    private static void WriteHeader(Stream stream, SphinxInventoryOptions options)
    {
        stream.Write(HeaderLine1);
        stream.Write(HeaderProjectPrefix);
        WriteUtf8Line(stream, options.ProjectName);
        stream.Write(HeaderVersionPrefix);
        WriteUtf8Line(stream, options.Version);
        stream.Write(HeaderCompressNote);
    }

    /// <summary>Encodes <paramref name="value"/> as UTF-8 and appends a single newline.</summary>
    /// <param name="stream">Output stream.</param>
    /// <param name="value">Source string.</param>
    private static void WriteUtf8Line(Stream stream, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var max = Encoding.UTF8.GetMaxByteCount(value.Length);
            var buffer = ArrayPool<byte>.Shared.Rent(max);
            try
            {
                var written = Encoding.UTF8.GetBytes(value, buffer);
                stream.Write(buffer.AsSpan(0, written));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        stream.WriteByte((byte)'\n');
    }

    /// <summary>Writes every entry into <paramref name="stream"/>, zlib-compressed.</summary>
    /// <param name="stream">Output stream (the compressed bytes are written directly to it).</param>
    /// <param name="entries">Snapshot entries.</param>
    private static void WriteCompressedBody(Stream stream, (byte[] Id, byte[] Url)[] entries)
    {
        using var zlib = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true);
        var sink = new ArrayBufferWriter<byte>(1024);
        for (var i = 0; i < entries.Length; i++)
        {
            WriteEntry(sink, entries[i].Id, entries[i].Url);
        }

        zlib.Write(sink.WrittenSpan);
    }

    /// <summary>Appends one <c>NAME std:label -1 URI -</c> line to <paramref name="sink"/>.</summary>
    /// <param name="sink">Body sink.</param>
    /// <param name="name">UID.</param>
    /// <param name="url">Resolved URL.</param>
    private static void WriteEntry(IBufferWriter<byte> sink, byte[] name, byte[] url)
    {
        sink.Write(name);
        sink.Write(EntryMiddle);
        sink.Write(url);
        sink.Write(EntryTrailer);
    }
}
