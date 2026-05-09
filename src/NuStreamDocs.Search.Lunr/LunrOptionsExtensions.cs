// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Lunr;

/// <summary>String / span construction helpers for the byte-shaped <see cref="LunrOptions"/> record.</summary>
/// <remarks>
/// Encodes the inputs once at construction so the per-page frontmatter extractor and the index
/// writer flow pure UTF-8. Callers building from configuration files reach for the string
/// overloads; callers with byte-literal sources can pass <c>[.. "..."u8]</c> directly.
/// </remarks>
public static class LunrOptionsExtensions
{
    /// <summary>Replaces the output subdirectory.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="subdirectory">Site-relative directory (e.g. <c>"search"</c>).</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithOutputSubdirectory(this LunrOptions options, PathSegment subdirectory) =>
        options with { OutputSubdirectory = subdirectory };

    /// <summary>Replaces the Lunr stop-word + stemmer language code.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="language">Language code (e.g. <c>"en"</c>, <c>"fr"</c>); empty for English default.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithLanguage(this LunrOptions options, string language) =>
        options with { Language = language ?? string.Empty };

    /// <summary>Replaces the minimum-token-length filter.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="minTokenLength">Documents shorter than this are dropped from the index.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithMinTokenLength(this LunrOptions options, int minTokenLength) =>
        options with { MinTokenLength = minTokenLength };

    /// <summary>Replaces the compression knob.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="compression">Compression strategy.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithCompression(this LunrOptions options, SearchCompression compression) =>
        options with { Compression = compression };

    /// <summary>Replaces the searchable-frontmatter-key list with <paramref name="keys"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithSearchableFrontmatterKeys(this LunrOptions options, params ApiCompatString[] keys) =>
        options with { SearchableFrontmatterKeys = keys.EncodeUtf8Array() };

    /// <summary>Replaces the searchable-frontmatter-key list with the supplied UTF-8 key bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key bytes.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithSearchableFrontmatterKeys(this LunrOptions options, params byte[][] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return options with { SearchableFrontmatterKeys = keys };
    }

    /// <summary>Appends <paramref name="keys"/> to the existing searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Additional frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions AddSearchableFrontmatterKeys(this LunrOptions options, params ApiCompatString[] keys)
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
    public static LunrOptions AddSearchableFrontmatterKeys(this LunrOptions options, params byte[][] keys)
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
    public static LunrOptions AddSearchableFrontmatterKeys(this LunrOptions options, ReadOnlySpan<byte> key) =>
        options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, [key.ToArray()]) };

    /// <summary>Empties the searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions ClearSearchableFrontmatterKeys(this LunrOptions options) =>
        options with { SearchableFrontmatterKeys = [] };

    /// <summary>Replaces the extra-stopword list with <paramref name="stopwords"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopwords">Stopword strings.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithExtraStopwords(this LunrOptions options, params ApiCompatString[] stopwords) =>
        options with { ExtraStopwords = stopwords.EncodeUtf8Array() };

    /// <summary>Replaces the extra-stopword list with the supplied UTF-8 stopword bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopwords">Stopword bytes.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithExtraStopwords(this LunrOptions options, params byte[][] stopwords)
    {
        ArgumentNullException.ThrowIfNull(stopwords);
        return options with { ExtraStopwords = stopwords };
    }

    /// <summary>Appends <paramref name="stopwords"/> to the existing extra-stopword list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="stopwords">Additional stopword strings.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions AddExtraStopwords(this LunrOptions options, params ApiCompatString[] stopwords)
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
    public static LunrOptions AddExtraStopwords(this LunrOptions options, params byte[][] stopwords)
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
    public static LunrOptions AddExtraStopwords(this LunrOptions options, ReadOnlySpan<byte> stopword) =>
        options with { ExtraStopwords = ArrayJoiner.Concat(options.ExtraStopwords, [stopword.ToArray()]) };

    /// <summary>Empties the extra-stopword list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions ClearExtraStopwords(this LunrOptions options) =>
        options with { ExtraStopwords = [] };

    /// <summary>Replaces the section-priority string with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Comma-separated <c>prefix:weight</c> pairs (e.g. <c>"documentation/:80,api/:-200"</c>).</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithSectionPriorities(this LunrOptions options, ApiCompatString value) =>
        options with { SectionPriorities = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the section-priority string with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 section-priority bytes.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithSectionPriorities(this LunrOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { SectionPriorities = value };
    }

    /// <summary>Replaces the section-priority string with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 section-priority bytes.</param>
    /// <returns>The updated options.</returns>
    public static LunrOptions WithSectionPriorities(this LunrOptions options, ReadOnlySpan<byte> value) =>
        options with { SectionPriorities = value.ToArray() };
}
