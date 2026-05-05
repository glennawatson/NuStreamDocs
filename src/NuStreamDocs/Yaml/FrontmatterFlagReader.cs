// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Yaml;

/// <summary>
/// Detects boolean build-pipeline flags in a page's YAML frontmatter
/// without parsing the whole document. Used by <c>PageDiscovery</c> to
/// decide whether a page should build (drafts) and whether the nav
/// tree should include it (<c>not_in_nav</c>).
/// </summary>
/// <remarks>
/// Reads at most the first ~512 bytes of the source file — enough to
/// cover the frontmatter region in every realistic page. The cost is
/// one syscall per page; on a 13.8K-page corpus the total adds ~50ms
/// to discovery.
/// </remarks>
public static class FrontmatterFlagReader
{
    /// <summary>Maximum bytes read off the front of each page; covers any plausible frontmatter region.</summary>
    private const int FrontmatterPeekBytes = 1024;

    /// <summary>Gets the UTF-8 bytes of <c>draft:</c>.</summary>
    private static ReadOnlySpan<byte> DraftKey => "draft:"u8;

    /// <summary>Gets the UTF-8 bytes of <c>not_in_nav:</c>.</summary>
    private static ReadOnlySpan<byte> NotInNavKey => "not_in_nav:"u8;

    /// <summary>Gets the UTF-8 bytes of the alternate spelling <c>nav_exclude:</c>.</summary>
    private static ReadOnlySpan<byte> NavExcludeKey => "nav_exclude:"u8;

    /// <summary>Reads <paramref name="absolutePath"/> and returns the detected flags.</summary>
    /// <param name="absolutePath">Absolute path to the markdown file.</param>
    /// <returns>The detected flags; <see cref="PageFlags.None"/> when there's no frontmatter or no flag keys.</returns>
    public static PageFlags Read(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(absolutePath);

        try
        {
            using var handle = File.OpenHandle(absolutePath);
            var size = (int)Math.Min(FrontmatterPeekBytes, RandomAccess.GetLength(handle));
            if (size <= 0)
            {
                return PageFlags.None;
            }

            Span<byte> buffer = stackalloc byte[FrontmatterPeekBytes];
            var read = RandomAccess.Read(handle, buffer[..size], 0);
            return ReadFlags(buffer[..read]);
        }
        catch (FileNotFoundException)
        {
            return PageFlags.None;
        }
        catch (DirectoryNotFoundException)
        {
            return PageFlags.None;
        }
    }

    /// <summary>Parses the flags from a peeked-at byte span.</summary>
    /// <param name="bytes">First N bytes of the source file.</param>
    /// <returns>The detected flags.</returns>
    public static PageFlags ReadFlags(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.StartsWith(YamlByteScanner.FrontmatterDelimiter))
        {
            return PageFlags.None;
        }

        var afterFirst = YamlByteScanner.FrontmatterDelimiter.Length;
        if (afterFirst >= bytes.Length || bytes[afterFirst] is not (byte)'\n' and not (byte)'\r')
        {
            return PageFlags.None;
        }

        var flags = PageFlags.None;
        var cursor = YamlByteScanner.LineEnd(bytes, 0);
        while (cursor < bytes.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(bytes, cursor);
            var line = bytes[cursor..lineEnd];
            var trimmed = line.TrimEnd((byte)'\n').TrimEnd((byte)'\r');
            if (trimmed.SequenceEqual(YamlByteScanner.FrontmatterDelimiter))
            {
                break;
            }

            flags |= ClassifyLine(line);
            cursor = lineEnd;
        }

        return flags;
    }

    /// <summary>Returns the flag bit(s) implied by one frontmatter line.</summary>
    /// <param name="line">Single frontmatter line.</param>
    /// <returns>Flag value or <see cref="PageFlags.None"/>.</returns>
    private static PageFlags ClassifyLine(ReadOnlySpan<byte> line)
    {
        if (line.StartsWith(DraftKey) && IsTrue(line[DraftKey.Length..]))
        {
            return PageFlags.Draft;
        }

        if ((line.StartsWith(NotInNavKey) && IsTrue(line[NotInNavKey.Length..])) ||
            (line.StartsWith(NavExcludeKey) && IsTrue(line[NavExcludeKey.Length..])))
        {
            return PageFlags.NotInNav;
        }

        return PageFlags.None;
    }

    /// <summary>Returns true when <paramref name="span"/> trims to a YAML truthy literal (<c>true</c>/<c>yes</c>).</summary>
    /// <param name="span">Bytes after the colon.</param>
    /// <returns>True for a truthy YAML scalar.</returns>
    private static bool IsTrue(ReadOnlySpan<byte> span)
    {
        var trimmed = YamlByteScanner.TrimWhitespace(span);
        return trimmed.SequenceEqual("true"u8) || trimmed.SequenceEqual("yes"u8);
    }
}
