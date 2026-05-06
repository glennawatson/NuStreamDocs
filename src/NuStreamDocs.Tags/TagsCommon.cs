// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Tags;

/// <summary>Shared byte-level helpers used by both the discovery-time tag plugin and the post-build index writer.</summary>
internal static class TagsCommon
{
    /// <summary>Initial-byte capacity hint for an emitted page; covers most pages without a resize.</summary>
    public const int PageInitialCapacity = 2 * 1024;

    /// <summary>Length of the <c>.md</c> source extension stripped before composing slugs / URLs.</summary>
    public const int MarkdownExtensionLength = 3;

    /// <summary>Length of the <c>.html</c> output extension.</summary>
    public const int HtmlExtensionLength = 5;

    /// <summary>OR-mask that maps an ASCII uppercase letter to its lowercase form.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Maximum tag length that fits in a stack slug buffer.</summary>
    private const int StackSlugLimit = 256;

    /// <summary>Translates a source-relative markdown path (e.g. <c>guide/intro.md</c>) to UTF-8 HTML URL bytes.</summary>
    /// <param name="markdownRelativePath">Source-relative path with platform separators.</param>
    /// <returns>UTF-8 forward-slashed bytes with the <c>.html</c> extension; an empty array when the input is empty.</returns>
    public static byte[] MdRelativePathToHtmlUrlBytes(ReadOnlySpan<char> markdownRelativePath)
    {
        if (markdownRelativePath.IsEmpty)
        {
            return [];
        }

        var endsWithMd = markdownRelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var keep = endsWithMd ? markdownRelativePath.Length - MarkdownExtensionLength : markdownRelativePath.Length;
        var totalLength = keep + (endsWithMd ? HtmlExtensionLength : 0);
        var dst = new byte[totalLength];
        for (var i = 0; i < keep; i++)
        {
            var c = markdownRelativePath[i];
            dst[i] = c is '\\' ? (byte)'/' : (byte)c;
        }

        if (endsWithMd)
        {
            ".html"u8.CopyTo(dst.AsSpan(keep));
        }

        return dst;
    }

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

    /// <summary>Builds a <c>{slug}{extension}</c> filename string in a single allocation.</summary>
    /// <param name="slug">ASCII slug bytes (alphanumeric / hyphen only).</param>
    /// <param name="extensionWithDot">Extension to append, including the leading dot (e.g. <c>".md"</c>, <c>".html"</c>).</param>
    /// <returns>The slug followed by the extension as a single allocated string.</returns>
    public static string BuildSlugFileName(byte[] slug, string extensionWithDot) =>
        string.Create(slug.Length + extensionWithDot.Length, (slug, extensionWithDot), static (dst, state) =>
        {
            var (src, ext) = state;
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = (char)src[i];
            }

            ext.AsSpan().CopyTo(dst[src.Length..]);
        });

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
