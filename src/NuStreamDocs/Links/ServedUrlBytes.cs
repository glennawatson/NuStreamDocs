// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Links;

/// <summary>
/// Maps source-relative paths to served-page URL bytes.
/// </summary>
public static class ServedUrlBytes
{
    /// <summary>Markdown extension length.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Forward slash byte.</summary>
    private const byte ForwardSlash = (byte)'/';

    /// <summary>Backslash byte.</summary>
    private const byte BackSlash = (byte)'\\';

    /// <summary>ASCII bit that distinguishes uppercase letters from lowercase letters.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Stack buffer limit for UTF-8 path encoding.</summary>
    private const int StackBufferLimit = 256;

    /// <summary>Gets the markdown extension bytes.</summary>
    private static ReadOnlySpan<byte> MdExtension => ".md"u8;

    /// <summary>Gets the html extension bytes.</summary>
    private static ReadOnlySpan<byte> HtmlExtension => ".html"u8;

    /// <summary>Gets the index file name bytes.</summary>
    private static ReadOnlySpan<byte> IndexFileName => "index"u8;

    /// <summary>Returns served URL bytes for <paramref name="path"/>.</summary>
    /// <param name="path">Source-relative path.</param>
    /// <param name="useDirectoryUrls">True for directory URLs.</param>
    /// <returns>UTF-8 URL bytes.</returns>
    public static byte[] FromPath(FilePath path, bool useDirectoryUrls) =>
        FromPath(path, useDirectoryUrls, leadingSlash: false);

    /// <summary>Returns served URL bytes for <paramref name="path"/>.</summary>
    /// <param name="path">Source-relative path.</param>
    /// <param name="useDirectoryUrls">True for directory URLs.</param>
    /// <param name="leadingSlash">True to prefix the result with <c>/</c>.</param>
    /// <returns>UTF-8 URL bytes.</returns>
    public static byte[] FromPath(FilePath path, bool useDirectoryUrls, bool leadingSlash)
    {
        if (path.IsEmpty)
        {
            return [];
        }

        var maxBytes = Encoding.UTF8.GetMaxByteCount(path.Value.Length);
        Span<byte> stackBuffer = stackalloc byte[StackBufferLimit];
        var encoded = maxBytes <= StackBufferLimit ? stackBuffer : new byte[maxBytes];
        var written = Encoding.UTF8.GetBytes(path.Value, encoded);
        return FromUtf8Path(encoded[..written], useDirectoryUrls, leadingSlash);
    }

    /// <summary>Returns the served URL bytes for an already UTF-8 encoded path.</summary>
    /// <param name="path">UTF-8 source-relative path.</param>
    /// <param name="useDirectoryUrls">True for directory URLs.</param>
    /// <param name="leadingSlash">True to prefix the result with <c>/</c>.</param>
    /// <returns>UTF-8 URL bytes.</returns>
    private static byte[] FromUtf8Path(ReadOnlySpan<byte> path, bool useDirectoryUrls, bool leadingSlash)
    {
        using var rental = PageBuilderPool.Rent(path.Length + HtmlExtension.Length + 2);
        var writer = rental.Writer;
        if (leadingSlash)
        {
            writer.Write("/"u8);
        }

        if (!EndsWithMarkdown(path))
        {
            WriteNormalized(path, writer);
            return [.. writer.WrittenSpan];
        }

        if (!useDirectoryUrls)
        {
            WriteNormalized(path[..^MarkdownExtensionLength], writer);
            writer.Write(HtmlExtension);
            return [.. writer.WrittenSpan];
        }

        var stem = path[..^MarkdownExtensionLength];
        var lastSlash = stem.LastIndexOfAny(ForwardSlash, BackSlash);
        var fileName = lastSlash >= 0 ? stem[(lastSlash + 1)..] : stem;
        if (!IsIndex(fileName))
        {
            WriteNormalized(stem, writer);
            writer.Write("/"u8);
            return [.. writer.WrittenSpan];
        }

        if (lastSlash >= 0)
        {
            WriteNormalized(stem[..(lastSlash + 1)], writer);
        }

        return [.. writer.WrittenSpan];
    }

    /// <summary>Returns true when <paramref name="path"/> ends with <c>.md</c> case-insensitively.</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <returns>True for markdown paths.</returns>
    private static bool EndsWithMarkdown(ReadOnlySpan<byte> path)
    {
        if (path.Length < MdExtension.Length)
        {
            return false;
        }

        var tail = path[^MdExtension.Length..];
        for (var i = 0; i < MdExtension.Length; i++)
        {
            var current = tail[i];
            if (current is >= (byte)'A' and <= (byte)'Z')
            {
                current = (byte)(current | AsciiCaseBit);
            }

            if (current != MdExtension[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns true when <paramref name="fileName"/> is <c>index</c>.</summary>
    /// <param name="fileName">UTF-8 stem bytes.</param>
    /// <returns>True for <c>index</c>.</returns>
    private static bool IsIndex(ReadOnlySpan<byte> fileName)
    {
        if (fileName.Length != IndexFileName.Length)
        {
            return false;
        }

        for (var i = 0; i < IndexFileName.Length; i++)
        {
            var current = fileName[i];
            if (current is >= (byte)'A' and <= (byte)'Z')
            {
                current = (byte)(current | AsciiCaseBit);
            }

            if (current != IndexFileName[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Writes <paramref name="path"/> into <paramref name="writer"/>, normalizing backslashes to forward slashes.</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <param name="writer">Destination writer.</param>
    private static void WriteNormalized(ReadOnlySpan<byte> path, ArrayBufferWriter<byte> writer)
    {
        var span = writer.GetSpan(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            span[i] = path[i] is BackSlash ? ForwardSlash : path[i];
        }

        writer.Advance(path.Length);
    }
}
