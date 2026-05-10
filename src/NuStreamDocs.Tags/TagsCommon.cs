// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Tags;

/// <summary>Shared helpers used by the tag plugin and the index writer.</summary>
internal static class TagsCommon
{
    /// <summary>Initial byte-capacity hint for an emitted page.</summary>
    public const int PageInitialCapacity = 2 * 1024;

    /// <summary>Length of the <c>.md</c> source extension stripped before composing slugs / URLs.</summary>
    public const int MarkdownExtensionLength = 3;

    /// <summary>Length of the <c>.html</c> output extension.</summary>
    public const int HtmlExtensionLength = 5;

    /// <summary>OR-mask that maps an ASCII uppercase letter to its lowercase form.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Maximum tag length that fits in a stack slug buffer.</summary>
    private const int StackSlugLimit = 256;

    /// <summary>Lowercases <paramref name="tag"/> and replaces non-alphanumeric ASCII runs with single hyphens for use as a filename.</summary>
    /// <param name="tag">UTF-8 tag display bytes.</param>
    /// <returns>UTF-8 filesystem-safe slug bytes; <c>"tag"</c> when the input has no slug-safe bytes.</returns>
    public static byte[] SlugifyTag(ReadOnlySpan<byte> tag)
    {
        if (tag.IsEmpty)
        {
            return [.. "tag"u8];
        }

        var stack = tag.Length <= StackSlugLimit ? stackalloc byte[tag.Length] : new byte[tag.Length];
        var written = SlugifyInto(tag, stack);
        return written is 0 ? [.. "tag"u8] : stack[..written].ToArray();
    }

    /// <summary>Builds a slug filename from the slug and extension.</summary>
    /// <param name="slug">ASCII slug bytes (alphanumeric / hyphen only).</param>
    /// <param name="extensionWithDot">Extension bytes to append, including the leading dot (e.g. <c>".md"u8</c>, <c>".html"u8</c>); ASCII.</param>
    /// <returns>The slug followed by the extension as a path-typed file name.</returns>
    public static FilePath BuildSlugFileName(ReadOnlySpan<byte> slug, ReadOnlySpan<byte> extensionWithDot)
    {
        var totalLength = slug.Length + extensionWithDot.Length;
        var fileName = string.Create(totalLength, (slug.ToArray(), extensionWithDot.ToArray()), static (dst, src) =>
        {
            var (slugBytes, extBytes) = src;
            for (var i = 0; i < slugBytes.Length; i++)
            {
                dst[i] = (char)slugBytes[i];
            }

            for (var i = 0; i < extBytes.Length; i++)
            {
                dst[slugBytes.Length + i] = (char)extBytes[i];
            }
        });
        return FilePath.FromString(fileName);
    }

    /// <summary>Writes the slug form of <paramref name="tag"/> into <paramref name="dst"/> and returns the count.</summary>
    /// <param name="tag">UTF-8 source bytes.</param>
    /// <param name="dst">Destination span.</param>
    /// <returns>Number of bytes written.</returns>
    private static int SlugifyInto(ReadOnlySpan<byte> tag, Span<byte> dst)
    {
        var count = 0;
        var pendingHyphen = false;
        for (var i = 0; i < tag.Length; i++)
        {
            var b = tag[i];
            switch (b)
            {
                case >= (byte)'A' and <= (byte)'Z':
                    {
                        count = FlushHyphen(dst, count, pendingHyphen);
                        dst[count++] = (byte)(b | AsciiCaseBit);
                        pendingHyphen = false;
                        continue;
                    }

                case >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9':
                    {
                        count = FlushHyphen(dst, count, pendingHyphen);
                        dst[count++] = b;
                        pendingHyphen = false;
                        continue;
                    }

                default:
                    {
                        pendingHyphen = count is not 0;
                        break;
                    }
            }
        }

        return count;
    }

    /// <summary>Appends a queued hyphen when one is pending and the buffer is non-empty.</summary>
    /// <param name="dst">Destination span.</param>
    /// <param name="count">Current count.</param>
    /// <param name="pendingHyphen">Whether a hyphen is queued.</param>
    /// <returns>Updated count.</returns>
    private static int FlushHyphen(Span<byte> dst, int count, bool pendingHyphen)
    {
        if (!pendingHyphen || count is 0)
        {
            return count;
        }

        dst[count] = (byte)'-';
        return count + 1;
    }
}
