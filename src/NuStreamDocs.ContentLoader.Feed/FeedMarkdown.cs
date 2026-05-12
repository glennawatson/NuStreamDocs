// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.ContentLoader.Feed;

/// <summary>Builds the Markdown bytes for a page synthesized from a feed item.</summary>
internal static class FeedMarkdown
{
    /// <summary>Assembles frontmatter (title / date / source / external URL) and the item body into a Markdown document.</summary>
    /// <param name="item">The feed item.</param>
    /// <param name="feedUrl">The feed URL the item came from.</param>
    /// <returns>UTF-8 Markdown source.</returns>
    public static byte[] Build(FeedItem item, ReadOnlySpan<byte> feedUrl)
    {
        ArrayBufferWriter<byte> writer = new(item.ContentHtml.Length + 256);
        writer.Write("---\n"u8);
        WriteScalar(writer, "title"u8, item.Title);
        WriteScalar(writer, "date"u8, item.Date);
        WriteScalar(writer, "source"u8, feedUrl);
        WriteScalar(writer, "external_url"u8, item.Link);
        writer.Write("---\n\n"u8);
        writer.Write(item.ContentHtml);
        if (writer.WrittenSpan is not [.., (byte)'\n'])
        {
            writer.Write("\n"u8);
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Writes a <c>key: "value"</c> frontmatter line with the value double-quote-escaped; skips empty values.</summary>
    /// <param name="writer">Destination.</param>
    /// <param name="key">Frontmatter key bytes.</param>
    /// <param name="value">Frontmatter value bytes.</param>
    private static void WriteScalar(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        writer.Write(key);
        writer.Write(": \""u8);
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if (b == (byte)'"')
            {
                writer.Write("\\\""u8);
            }
            else if (b == (byte)'\\')
            {
                writer.Write("\\\\"u8);
            }
            else if (b is (byte)'\n' or (byte)'\r')
            {
                writer.Write(" "u8);
            }
            else
            {
                WriteByte(writer, b);
            }
        }

        writer.Write("\"\n"u8);
    }

    /// <summary>Appends a single byte to <paramref name="writer"/>.</summary>
    /// <param name="writer">Destination.</param>
    /// <param name="b">Byte to append.</param>
    private static void WriteByte(ArrayBufferWriter<byte> writer, byte b)
    {
        var span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }
}
