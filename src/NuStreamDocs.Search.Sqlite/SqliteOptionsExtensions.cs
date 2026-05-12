// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>Fluent helpers for building <see cref="SqliteOptions"/>.</summary>
public static class SqliteOptionsExtensions
{
    /// <summary>Replaces the output subdirectory.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="subdirectory">Site-relative directory (e.g. <c>"search"</c>).</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithOutputSubdirectory(this in SqliteOptions options, in PathSegment subdirectory) =>
        options with { OutputSubdirectory = subdirectory };

    /// <summary>Replaces the minimum-token-length filter.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="minTokenLength">Documents shorter than this are dropped from the index.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithMinTokenLength(this in SqliteOptions options, int minTokenLength) =>
        options with { MinTokenLength = minTokenLength };

    /// <summary>Replaces the searchable-frontmatter-key list with <paramref name="keys"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithSearchableFrontmatterKeys(this in SqliteOptions options, params ApiCompatString[] keys) =>
        options with { SearchableFrontmatterKeys = keys.EncodeUtf8Array() };

    /// <summary>Replaces the searchable-frontmatter-key list with the supplied UTF-8 key bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithSearchableFrontmatterKeys(this in SqliteOptions options, params byte[][] keys)
    {
        return options with { SearchableFrontmatterKeys = keys };
    }

    /// <summary>Appends <paramref name="keys"/> to the existing searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Additional frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions AddSearchableFrontmatterKeys(this in SqliteOptions options, params ApiCompatString[] keys)
    {
        return keys.Length is 0
            ? options
            : options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, keys.EncodeUtf8Array()) };
    }

    /// <summary>Appends UTF-8 <paramref name="keys"/> to the existing searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Additional frontmatter key bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions AddSearchableFrontmatterKeys(this in SqliteOptions options, params byte[][] keys)
    {
        return keys.Length is 0
            ? options
            : options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, keys) };
    }

    /// <summary>Appends a single UTF-8 frontmatter key (e.g. a <c>"..."u8</c> literal) to the existing list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="key">UTF-8 frontmatter-key bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions AddSearchableFrontmatterKeys(this in SqliteOptions options, ReadOnlySpan<byte> key) =>
        options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, [key.ToArray()]) };

    /// <summary>Empties the searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions ClearSearchableFrontmatterKeys(this in SqliteOptions options) =>
        options with { SearchableFrontmatterKeys = [] };

    /// <summary>Replaces the excluded-prefix list with <paramref name="prefixes"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="prefixes">Root-relative URL prefix strings (e.g. <c>"api/"</c>).</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithExcludePathPrefixes(this in SqliteOptions options, params ApiCompatString[] prefixes) =>
        options with { ExcludePathPrefixes = prefixes.EncodeUtf8Array() };

    /// <summary>Replaces the excluded-prefix list with the supplied UTF-8 prefix bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="prefixes">UTF-8 prefix bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithExcludePathPrefixes(this in SqliteOptions options, params byte[][] prefixes)
    {
        return options with { ExcludePathPrefixes = prefixes };
    }

    /// <summary>Appends <paramref name="prefixes"/> to the existing excluded-prefix list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="prefixes">Additional prefix strings.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions AddExcludePathPrefixes(this in SqliteOptions options, params ApiCompatString[] prefixes)
    {
        return prefixes.Length is 0
            ? options
            : options with { ExcludePathPrefixes = ArrayJoiner.Concat(options.ExcludePathPrefixes, prefixes.EncodeUtf8Array()) };
    }

    /// <summary>Appends UTF-8 <paramref name="prefixes"/> to the existing excluded-prefix list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="prefixes">Additional prefix bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions AddExcludePathPrefixes(this in SqliteOptions options, params byte[][] prefixes)
    {
        return prefixes.Length is 0
            ? options
            : options with { ExcludePathPrefixes = ArrayJoiner.Concat(options.ExcludePathPrefixes, prefixes) };
    }

    /// <summary>Appends a single UTF-8 prefix (e.g. a <c>"..."u8</c> literal) to the existing list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="prefix">UTF-8 prefix bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions AddExcludePathPrefixes(this in SqliteOptions options, ReadOnlySpan<byte> prefix) =>
        options with { ExcludePathPrefixes = ArrayJoiner.Concat(options.ExcludePathPrefixes, [prefix.ToArray()]) };

    /// <summary>Empties the excluded-prefix list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions ClearExcludePathPrefixes(this in SqliteOptions options) =>
        options with { ExcludePathPrefixes = [] };

    /// <summary>Sets whether the full page body is stored in the index (true) or just a short leading excerpt (false).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="indexFullBody">True to store the whole body; false to store only a short leading excerpt.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithIndexFullBody(this in SqliteOptions options, bool indexFullBody) =>
        options with { IndexFullBody = indexFullBody };

    /// <summary>Replaces the section-priority string with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Comma-separated <c>prefix:weight</c> pairs (e.g. <c>"documentation/:80,api/:-200"</c>).</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithSectionPriorities(this in SqliteOptions options, in ApiCompatString value) =>
        options with { SectionPriorities = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the section-priority string with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 section-priority bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithSectionPriorities(this in SqliteOptions options, byte[] value)
    {
        return options with { SectionPriorities = value };
    }

    /// <summary>Replaces the section-priority string with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 section-priority bytes.</param>
    /// <returns>The updated options.</returns>
    public static SqliteOptions WithSectionPriorities(this in SqliteOptions options, ReadOnlySpan<byte> value) =>
        options with { SectionPriorities = value.ToArray() };
}
