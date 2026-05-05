// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.Internal;

/// <summary>UTF-8 HTML entity escaper shared across preprocessors.</summary>
internal static class HtmlEscaper
{
    /// <summary>Writes <paramref name="value"/> with <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>"</c> escaped.</summary>
    /// <param name="value">UTF-8 input.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Escape(ReadOnlySpan<byte> value, IBufferWriter<byte> writer)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            var entity = b switch
            {
                (byte)'&' => "&amp;"u8,
                (byte)'<' => "&lt;"u8,
                (byte)'>' => "&gt;"u8,
                (byte)'"' => "&quot;"u8,
                _ => default
            };

            if (entity.Length is 0)
            {
                var dest = writer.GetSpan(1);
                dest[0] = b;
                writer.Advance(1);
            }
            else
            {
                writer.Write(entity);
            }
        }
    }
}
