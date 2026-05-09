// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>String / span construction helpers for the byte-shaped <see cref="PagefindOptions"/> record.</summary>
/// <remarks>
/// Encodes the inputs once at construction so the per-page frontmatter extractor and the index
/// writer flow pure UTF-8. Callers building from configuration files reach for the string
/// overloads; callers with byte-literal sources can pass <c>[.. "..."u8]</c> directly.
/// </remarks>
public static class PagefindOptionsExtensions
{
    /// <summary>Replaces the output subdirectory.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="subdirectory">Site-relative directory (e.g. <c>"search"</c>).</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithOutputSubdirectory(this PagefindOptions options, PathSegment subdirectory) =>
        options with { OutputSubdirectory = subdirectory };

    /// <summary>Replaces the minimum-token-length filter.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="minTokenLength">Documents shorter than this are dropped from the index.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithMinTokenLength(this PagefindOptions options, int minTokenLength) =>
        options with { MinTokenLength = minTokenLength };

    /// <summary>Replaces the searchable-frontmatter-key list with <paramref name="keys"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithSearchableFrontmatterKeys(this PagefindOptions options, params ApiCompatString[] keys) =>
        options with { SearchableFrontmatterKeys = keys.EncodeUtf8Array() };

    /// <summary>Replaces the searchable-frontmatter-key list with the supplied UTF-8 key bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Frontmatter key bytes.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithSearchableFrontmatterKeys(this PagefindOptions options, params byte[][] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return options with { SearchableFrontmatterKeys = keys };
    }

    /// <summary>Appends <paramref name="keys"/> to the existing searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="keys">Additional frontmatter key strings.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions AddSearchableFrontmatterKeys(this PagefindOptions options, params ApiCompatString[] keys)
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
    public static PagefindOptions AddSearchableFrontmatterKeys(this PagefindOptions options, params byte[][] keys)
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
    public static PagefindOptions AddSearchableFrontmatterKeys(this PagefindOptions options, ReadOnlySpan<byte> key) =>
        options with { SearchableFrontmatterKeys = ArrayJoiner.Concat(options.SearchableFrontmatterKeys, [key.ToArray()]) };

    /// <summary>Empties the searchable-frontmatter-key list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions ClearSearchableFrontmatterKeys(this PagefindOptions options) =>
        options with { SearchableFrontmatterKeys = [] };

    /// <summary>Replaces the section-priority string with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Comma-separated <c>prefix:weight</c> pairs (e.g. <c>"documentation/:80,api/:-200"</c>).</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithSectionPriorities(this PagefindOptions options, ApiCompatString value) =>
        options with { SectionPriorities = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the section-priority string with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 section-priority bytes.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithSectionPriorities(this PagefindOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { SectionPriorities = value };
    }

    /// <summary>Replaces the section-priority string with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 section-priority bytes.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithSectionPriorities(this PagefindOptions options, ReadOnlySpan<byte> value) =>
        options with { SectionPriorities = value.ToArray() };

    /// <summary>Toggles whether the Pagefind CLI binary runs against the rendered output to produce the WASM runtime + binary inverted-index shards.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="enabled">True (default) invokes the binary; false ships JSON only.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithRunCli(this PagefindOptions options, bool enabled) =>
        options with { RunCli = enabled };

    /// <summary>Overrides the resolved Pagefind binary path. Pass <c>default</c> to fall back to per-RID auto-detection.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="binaryPath">Absolute path to a <c>pagefind</c> executable.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithBinaryPath(this PagefindOptions options, FilePath binaryPath) =>
        options with { BinaryPath = binaryPath };

    /// <summary>Flips missing-binary / non-zero-exit handling from "warn" to "throw". Use in CI publishes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="strict">True to throw on missing/failed binary.</param>
    /// <returns>The updated options.</returns>
    public static PagefindOptions WithStrictBinaryRequired(this PagefindOptions options, bool strict) =>
        options with { StrictBinaryRequired = strict };
}
