// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>Configuration for <c>SqliteSearchPlugin</c>.</summary>
/// <param name="OutputSubdirectory">Site-relative directory the <c>search.db</c> is written under (e.g. <c>search</c>).</param>
/// <param name="MinTokenLength">Documents whose extracted text is shorter than this are dropped from the index.</param>
/// <param name="SearchableFrontmatterKeys">UTF-8 frontmatter keys whose values are folded into each page's searchable text. Empty for body-only indexing.</param>
/// <param name="ExcludePathPrefixes">UTF-8 root-relative URL prefixes (e.g. <c>api/</c>) whose pages are excluded from the index. Empty indexes every rendered page.</param>
/// <param name="IndexFullBody">
/// When true, the whole stripped page text is stored in the index for snippet rendering; when false only the title plus a short leading
/// excerpt is stored, shrinking the database on body-heavy sites.
/// </param>
/// <param name="SectionPriorities">UTF-8 comma-separated <c>prefix:weight</c> pairs that bias result ranking by URL prefix; empty disables section weighting.</param>
public readonly record struct SqliteOptions(
    PathSegment OutputSubdirectory,
    int MinTokenLength,
    byte[][] SearchableFrontmatterKeys,
    byte[][] ExcludePathPrefixes,
    bool IndexFullBody,
    byte[] SectionPriorities)
{
    /// <summary>
    /// The default minimum token length.
    /// </summary>
    private const int DefaultTokenLength = 3;

    /// <summary>Gets the maximum stored body length, in UTF-8 bytes, when <see cref="IndexFullBody"/> is false.</summary>
    public static int ExcerptByteLimit { get; } = 300;

    /// <summary>Gets the option set with all defaults populated.</summary>
    public static SqliteOptions Default { get; } = new(
        "search",
        DefaultTokenLength,
        [],
        [],
        true,
        []);
}
