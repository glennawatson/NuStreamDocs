// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search;

/// <summary>String / span construction helpers for the byte-shaped <see cref="SearchOptions"/> record.</summary>
/// <remarks>
/// Encodes the inputs once at construction so the per-page frontmatter extractor and the index
/// writer flow pure UTF-8. Callers building from configuration files reach for the string
/// overloads; callers with byte-literal sources can pass <c>"..."u8.ToArray()</c> directly.
/// </remarks>
public static class SearchOptionsExtensions
{
    /// <summary>Replaces the searchable-frontmatter-key list with <paramref name="keys"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions WithSearchableFrontmatterKeys(this SearchOptions options, params ApiCompatString[] keys) =>
        options with { SearchableFrontmatterKeys = keys.EncodeUtf8Array() };

    /// <summary>Replaces the searchable-frontmatter-key list with the supplied UTF-8 key bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key bytes.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions WithSearchableFrontmatterKeys(this SearchOptions options, params byte[][] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return options with { SearchableFrontmatterKeys = keys };
    }

    /// <summary>Appends <paramref name="keys"/> to the existing searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Additional frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions AddSearchableFrontmatterKeys(this SearchOptions options, params ApiCompatString[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.Length is 0
            ? options
            : options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, keys.EncodeUtf8Array()) };
    }

    /// <summary>Appends UTF-8 <paramref name="keys"/> to the existing searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Additional frontmatter key bytes.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions AddSearchableFrontmatterKeys(this SearchOptions options, params byte[][] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.Length is 0
            ? options
            : options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, keys) };
    }

    /// <summary>Appends a single UTF-8 frontmatter key (e.g. a <c>"..."u8</c> literal) to the existing list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="key">UTF-8 frontmatter-key bytes.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions AddSearchableFrontmatterKeys(this SearchOptions options, ReadOnlySpan<byte> key) =>
        options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, [key.ToArray()]) };

    /// <summary>Empties the searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions ClearSearchableFrontmatterKeys(this SearchOptions options) =>
        options with { SearchableFrontmatterKeys = [] };

    /// <summary>Replaces the extra-stopword list with <paramref name="stopwords"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopwords">Stopword strings.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions WithExtraStopwords(this SearchOptions options, params ApiCompatString[] stopwords) =>
        options with { ExtraStopwords = stopwords.EncodeUtf8Array() };

    /// <summary>Replaces the extra-stopword list with the supplied UTF-8 stopword bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopwords">Stopword bytes.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions WithExtraStopwords(this SearchOptions options, params byte[][] stopwords)
    {
        ArgumentNullException.ThrowIfNull(stopwords);
        return options with { ExtraStopwords = stopwords };
    }

    /// <summary>Appends <paramref name="stopwords"/> to the existing extra-stopword list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopwords">Additional stopword strings.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions AddExtraStopwords(this SearchOptions options, params ApiCompatString[] stopwords)
    {
        ArgumentNullException.ThrowIfNull(stopwords);
        return stopwords.Length is 0
            ? options
            : options with { ExtraStopwords = ArrayJoiner.Concat(options.ExtraStopwords, stopwords.EncodeUtf8Array()) };
    }

    /// <summary>Appends UTF-8 <paramref name="stopwords"/> to the existing extra-stopword list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopwords">Additional stopword bytes.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions AddExtraStopwords(this SearchOptions options, params byte[][] stopwords)
    {
        ArgumentNullException.ThrowIfNull(stopwords);
        return stopwords.Length is 0
            ? options
            : options with { ExtraStopwords = ArrayJoiner.Concat(options.ExtraStopwords, stopwords) };
    }

    /// <summary>Appends a single UTF-8 stopword (e.g. a <c>"..."u8</c> literal) to the existing list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopword">UTF-8 stopword bytes.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions AddExtraStopwords(this SearchOptions options, ReadOnlySpan<byte> stopword) =>
        options with { ExtraStopwords = ArrayJoiner.Concat(options.ExtraStopwords, [stopword.ToArray()]) };

    /// <summary>Empties the extra-stopword list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static SearchOptions ClearExtraStopwords(this SearchOptions options) =>
        options with { ExtraStopwords = [] };
}
