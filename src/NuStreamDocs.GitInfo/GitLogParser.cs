// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.GitInfo;

/// <summary>Pure UTF-8 parser for batched <c>git log</c> streams and front-matter date/author overrides.</summary>
internal static class GitLogParser
{
    /// <summary>ASCII record separator (<c>%x1e</c>) preceding each commit header.</summary>
    private const byte RecordSeparator = 0x1E;

    /// <summary>ASCII unit separator (<c>%x1f</c>) separating commit date and author name.</summary>
    private const byte UnitSeparator = 0x1F;

    /// <summary>Byte length of a leading <c>./</c> path prefix.</summary>
    private const int DotSlashPrefixLength = 2;

    /// <summary>Parses a <c>--format=%x1e%ad%x1f%an --name-only</c> byte stream into per-file <see cref="GitPageMetadata"/>.</summary>
    /// <param name="gitLogBytes">Raw UTF-8 <c>git log</c> output.</param>
    /// <param name="maxContributors">Maximum distinct contributors retained per file.</param>
    /// <returns>Lookup keyed by forward-slashed UTF-8 relative path.</returns>
    internal static Dictionary<byte[], GitPageMetadata> ParseLog(ReadOnlySpan<byte> gitLogBytes, int maxContributors)
    {
        Dictionary<byte[], Accumulator> builders = [with(ByteArrayComparer.Instance)];
        var cursor = 0;
        while (cursor < gitLogBytes.Length)
        {
            var relStart = gitLogBytes[cursor..].IndexOf(RecordSeparator);
            if (relStart < 0)
            {
                break;
            }

            var recordStart = cursor + relStart + 1;
            var nextRel = gitLogBytes[recordStart..].IndexOf(RecordSeparator);
            var recordEnd = nextRel < 0 ? gitLogBytes.Length : recordStart + nextRel;
            ParseCommitRecord(gitLogBytes[recordStart..recordEnd], maxContributors, builders);
            cursor = recordEnd;
        }

        Dictionary<byte[], GitPageMetadata> result = [with(builders.Count, ByteArrayComparer.Instance)];
        foreach (var pair in builders)
        {
            result[pair.Key] = new(pair.Value.LastDate, [.. pair.Value.Authors]);
        }

        return result;
    }

    /// <summary>Resolves metadata for a single page by combining front-matter overrides with parsed Git history.</summary>
    /// <param name="relativePath">Page path relative to the docs root.</param>
    /// <param name="source">UTF-8 Markdown source.</param>
    /// <param name="gitMap">Parsed Git history table.</param>
    /// <param name="maxContributors">Maximum contributor count.</param>
    /// <param name="metadata">Resolved metadata when found.</param>
    /// <returns><c>true</c> when at least a revision date was resolved; otherwise, <c>false</c>.</returns>
    internal static bool TryResolvePage(
        in FilePath relativePath,
        ReadOnlySpan<byte> source,
        Dictionary<byte[], GitPageMetadata> gitMap,
        int maxContributors,
        out GitPageMetadata metadata)
    {
        metadata = default;
        if (relativePath.IsEmpty || IsOptedOut(source))
        {
            return false;
        }

        var pathKey = NormalizePathUtf8(relativePath.AsSpan());
        var hasGit = gitMap.TryGetValue(pathKey, out var gitEntry);

        var frontDate = ReadFrontmatterDate(source);
        var fallbackDate = hasGit ? gitEntry.LastUpdatedIsoDate : [];
        var resolvedDate = !frontDate.IsEmpty ? frontDate.ToArray() : fallbackDate;
        if (resolvedDate.Length is 0)
        {
            return false;
        }

        var frontAuthor = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "author"u8));
        var contributors = MergeContributors(frontAuthor, hasGit ? gitEntry.Contributors : [], maxContributors);
        metadata = new(resolvedDate, contributors);
        return true;
    }

    /// <summary>Normalizes a character path span to forward-slashed UTF-8 bytes without leading <c>./</c>.</summary>
    /// <param name="path">Relative path characters.</param>
    /// <returns>Normalized UTF-8 path bytes.</returns>
    internal static byte[] NormalizePathUtf8(ReadOnlySpan<char> path)
    {
        var utf8 = Encoding.UTF8.GetBytes(path.ToArray());
        for (var i = 0; i < utf8.Length; i++)
        {
            if (utf8[i] is (byte)'\\')
            {
                utf8[i] = (byte)'/';
            }
        }

        var span = utf8.AsSpan();
        while (span.StartsWith("./"u8))
        {
            span = span[DotSlashPrefixLength..];
        }

        return span.Length == utf8.Length ? utf8 : span.ToArray();
    }

    /// <summary>Parses a single commit block (header line + touched file lines).</summary>
    /// <param name="record">Commit block bytes after <c>0x1E</c>.</param>
    /// <param name="maxContributors">Contributor cap.</param>
    /// <param name="builders">Per-file accumulator table.</param>
    private static void ParseCommitRecord(
        ReadOnlySpan<byte> record,
        int maxContributors,
        Dictionary<byte[], Accumulator> builders)
    {
        var headerEnd = Utf8LineSpan.LfLineEnd(record, 0);
        var header = YamlByteScanner.TrimWhitespace(record[..headerEnd]);
        var unitOffset = header.IndexOf(UnitSeparator);
        if (unitOffset <= 0)
        {
            return;
        }

        var dateSpan = YamlByteScanner.TrimWhitespace(header[..unitOffset]);
        var authorSpan = YamlByteScanner.TrimWhitespace(header[(unitOffset + 1)..]);

        var cursor = headerEnd;
        while (cursor < record.Length)
        {
            var lineEnd = Utf8LineSpan.LfLineEnd(record, cursor);
            var rawPath = YamlByteScanner.TrimWhitespace(record[cursor..lineEnd]);
            cursor = lineEnd;
            if (rawPath.IsEmpty)
            {
                continue;
            }

            RecordFileTouch(rawPath, dateSpan, authorSpan, maxContributors, builders);
        }
    }

    /// <summary>Updates the accumulator for <paramref name="rawPath"/> with a commit's date and author.</summary>
    /// <param name="rawPath">UTF-8 relative file path from <c>git log</c>.</param>
    /// <param name="dateSpan">Commit ISO date.</param>
    /// <param name="authorSpan">Commit author name.</param>
    /// <param name="maxContributors">Contributor cap.</param>
    /// <param name="builders">Accumulator table.</param>
    private static void RecordFileTouch(
        ReadOnlySpan<byte> rawPath,
        ReadOnlySpan<byte> dateSpan,
        ReadOnlySpan<byte> authorSpan,
        int maxContributors,
        Dictionary<byte[], Accumulator> builders)
    {
        var lookup = builders.GetAlternateLookup<ReadOnlySpan<byte>>();
        if (!lookup.TryGetValue(rawPath, out var acc))
        {
            acc = new(dateSpan.ToArray());
            lookup[rawPath] = acc;
        }

        if (authorSpan.IsEmpty || acc.Authors.Count >= maxContributors || ContainsSpan(acc.Authors, authorSpan))
        {
            return;
        }

        acc.Authors.Add(authorSpan.ToArray());
    }

    /// <summary>Returns <c>true</c> when <paramref name="items"/> already contains bytes matching <paramref name="candidate"/>.</summary>
    /// <param name="items">Existing UTF-8 items.</param>
    /// <param name="candidate">Candidate bytes.</param>
    /// <returns><c>true</c> when already present.</returns>
    private static bool ContainsSpan(List<byte[]> items, ReadOnlySpan<byte> candidate)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].AsSpan().SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns <c>true</c> when front matter sets <c>git_info: false</c>.</summary>
    /// <param name="source">UTF-8 Markdown source.</param>
    /// <returns><c>true</c> when opted out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOptedOut(ReadOnlySpan<byte> source)
    {
        var flag = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "git_info"u8));
        return !flag.IsEmpty && AsciiByteHelpers.EqualsIgnoreAsciiCase(flag, "false"u8);
    }

    /// <summary>Reads <c>last_updated:</c> or <c>date:</c> from YAML front matter.</summary>
    /// <param name="source">UTF-8 Markdown source.</param>
    /// <returns>Unquoted date slice, or empty when absent.</returns>
    private static ReadOnlySpan<byte> ReadFrontmatterDate(ReadOnlySpan<byte> source)
    {
        var updated = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "last_updated"u8));
        return !updated.IsEmpty
            ? updated
            : YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "date"u8));
    }

    /// <summary>Combines an optional front-matter author with Git contributors up to <paramref name="maxContributors"/>.</summary>
    /// <param name="frontAuthor">Optional front-matter author bytes.</param>
    /// <param name="gitContributors">Git contributor list.</param>
    /// <param name="maxContributors">Maximum contributor count.</param>
    /// <returns>Merged contributor array.</returns>
    private static byte[][] MergeContributors(
        ReadOnlySpan<byte> frontAuthor,
        byte[][] gitContributors,
        int maxContributors)
    {
        if (frontAuthor.IsEmpty)
        {
            return gitContributors.Length <= maxContributors
                ? gitContributors
                : gitContributors.AsSpan(0, maxContributors).ToArray();
        }

        List<byte[]> merged = [frontAuthor.ToArray()];
        for (var i = 0; i < gitContributors.Length && merged.Count < maxContributors; i++)
        {
            if (!ContainsSpan(merged, gitContributors[i]))
            {
                merged.Add(gitContributors[i]);
            }
        }

        return [.. merged];
    }

    /// <summary>Mutable per-file accumulator used while scanning commits.</summary>
    /// <param name="lastDate">Most recent ISO-8601 date seen for the file.</param>
    [System.Diagnostics.DebuggerDisplay("Accumulator: {Authors.Count}")]
    private sealed class Accumulator(byte[] lastDate)
    {
        /// <summary>Gets the most recent commit date.</summary>
        public byte[] LastDate { get; } = lastDate;

        /// <summary>Gets the ordered distinct contributor list.</summary>
        public List<byte[]> Authors { get; } = [];
    }
}
