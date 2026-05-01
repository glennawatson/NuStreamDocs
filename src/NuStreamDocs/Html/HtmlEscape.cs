// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.Html;

/// <summary>
/// UTF-8 HTML escaper.
/// </summary>
/// <remarks>
/// Operates entirely on bytes — no string/UTF-16 round trip. The hot
/// path advances over runs of unescaped bytes via vectorised
/// <c>IndexOfAny(SearchValues&lt;byte&gt;)</c> and writes them in bulk to
/// the destination, falling through to a per-byte switch only at
/// escape points.
/// </remarks>
public static class HtmlEscape
{
    /// <summary>Bytes that require an HTML entity replacement in text content.</summary>
    private static readonly SearchValues<byte> EscapeBytes =
        SearchValues.Create("&<>\""u8);

    /// <summary>
    /// Writes <paramref name="source"/> to <paramref name="writer"/>,
    /// replacing <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, and <c>&quot;</c>
    /// with their named HTML entities.
    /// </summary>
    /// <param name="source">UTF-8 input span.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void EscapeText(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (source.IsEmpty)
        {
            return;
        }

        // Hint the writer to fit at least the source bytes verbatim — the
        // common case (no escapable bytes) writes exactly source.Length and
        // a writer pre-sized by the caller skips the grow entirely. The
        // previous worst-case 6× hint forced a multi-hundred-KB upfront
        // allocation on every clean page. The per-byte WriteEntity loop
        // grows the buffer further on the heavy path naturally.
        _ = writer.GetSpan(source.Length);

        var cursor = source;
        while (!cursor.IsEmpty)
        {
            var idx = cursor.IndexOfAny(EscapeBytes);
            switch (idx)
            {
                case < 0:
                    {
                        CopyTo(cursor, writer);
                        return;
                    }

                case > 0:
                    {
                        CopyTo(cursor[..idx], writer);
                        break;
                    }
            }

            WriteEntity(cursor[idx], writer);
            cursor = cursor[(idx + 1)..];
        }
    }

    /// <summary>Bulk-copies <paramref name="src"/> into <paramref name="writer"/>.</summary>
    /// <param name="src">UTF-8 bytes to copy verbatim.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyTo(ReadOnlySpan<byte> src, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(src.Length);
        src.CopyTo(dst);
        writer.Advance(src.Length);
    }

    /// <summary>Writes the entity replacement for <paramref name="ch"/>.</summary>
    /// <param name="ch">One of <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&quot;</c>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteEntity(byte ch, IBufferWriter<byte> writer)
    {
        var entity = ch switch
        {
            (byte)'&' => "&amp;"u8,
            (byte)'<' => "&lt;"u8,
            (byte)'>' => "&gt;"u8,
            (byte)'"' => "&quot;"u8,
            _ => default,
        };

        if (entity.IsEmpty)
        {
            var dst = writer.GetSpan(1);
            dst[0] = ch;
            writer.Advance(1);
            return;
        }

        CopyTo(entity, writer);
    }
}
