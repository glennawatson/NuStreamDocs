// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Common;

/// <summary>
/// Streams a UTF-8 byte span to an <see cref="IBufferWriter{T}"/> while
/// expanding XML/HTML special bytes (<c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>,
/// optionally <c>&quot;</c>) into named entities.
/// </summary>
public static class XmlEntityEscaper
{
    /// <summary>Escape mode controlling which bytes get expanded.</summary>
    public enum Mode
    {
        /// <summary>Minimal XML element-content escape: <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>.</summary>
        Xml,

        /// <summary>HTML attribute-safe escape: adds <c>&quot;</c> to the minimal set.</summary>
        HtmlAttribute
    }

    /// <summary>Streams <paramref name="bytes"/> to <paramref name="writer"/>, replacing reserved bytes with the named entities for <paramref name="mode"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">UTF-8 source bytes.</param>
    /// <param name="mode">Which entity set to apply.</param>
    public static void WriteEscaped(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes, Mode mode)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var runStart = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var entity = EntityFor(bytes[i], mode);
            if (entity.IsEmpty)
            {
                continue;
            }

            if (i > runStart)
            {
                writer.Write(bytes[runStart..i]);
            }

            writer.Write(entity);
            runStart = i + 1;
        }

        if (runStart >= bytes.Length)
        {
            return;
        }

        writer.Write(bytes[runStart..]);
    }

    /// <summary>Returns the entity bytes for <paramref name="b"/> under <paramref name="mode"/>, or empty when the byte is plain.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <param name="mode">Active escape mode.</param>
    /// <returns>Entity bytes or empty.</returns>
    private static ReadOnlySpan<byte> EntityFor(byte b, Mode mode) => b switch
    {
        (byte)'&' => "&amp;"u8,
        (byte)'<' => "&lt;"u8,
        (byte)'>' => "&gt;"u8,
        (byte)'"' when mode is Mode.HtmlAttribute => "&quot;"u8,
        _ => default
    };
}
