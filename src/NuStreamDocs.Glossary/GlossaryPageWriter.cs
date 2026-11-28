// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Glossary;

/// <summary>Formats sorted <see cref="GlossaryEntry"/> records into a synthetic Markdown glossary page.</summary>
internal static class GlossaryPageWriter
{
    /// <summary>Writes the glossary Markdown document into <paramref name="writer"/>.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="title">UTF-8 page title.</param>
    /// <param name="entries">Alphabetically sorted glossary entries.</param>
    internal static void WriteMarkdown(IBufferWriter<byte> writer, ReadOnlySpan<byte> title, ReadOnlySpan<GlossaryEntry> entries)
    {
        writer.Write("---\ntitle: \""u8);
        WriteYamlDoubleQuoted(writer, title);
        writer.Write("\"\n---\n\n# "u8);
        writer.Write(title);
        writer.Write("\n\n"u8);

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            writer.Write("## "u8);
            writer.Write(entry.Term);
            writer.Write("\n\n"u8);
            writer.Write(entry.Definition);
            writer.Write("\n\n"u8);
        }
    }

    /// <summary>Writes <paramref name="value"/> escaping <c>"</c> and <c>\</c> for YAML frontmatter.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="value">Raw UTF-8 scalar.</param>
    private static void WriteYamlDoubleQuoted(IBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if (b is (byte)'"' or (byte)'\\')
            {
                writer.Write("\\"u8);
            }

            var dst = writer.GetSpan(1);
            dst[0] = b;
            writer.Advance(1);
        }
    }
}
