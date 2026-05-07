// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search;

/// <summary>Configuration for <see cref="SearchPlugin"/>.</summary>
/// <remarks>
/// <see cref="SearchableFrontmatterKeys"/> / <see cref="ExtraStopwords"/> are stored as UTF-8 bytes
/// per the project's byte-first pipeline rule — the per-page frontmatter extractor and the index
/// writer both consume bytes directly. String-shaped construction goes through
/// <c>SearchOptionsExtensions</c>'s <c>WithXxx</c> / <c>AddXxx</c> helpers, which encode once at
/// the boundary.
/// </remarks>
/// <param name="Format">On-disk index format.</param>
/// <param name="OutputSubdirectory">Site-relative directory the index files are written under (e.g. <c>search</c>).</param>
/// <param name="Language">Stop-word + stemmer language code (Lunr only); empty for English default.</param>
/// <param name="MinTokenLength">Tokens shorter than this are dropped from the index. Reduces index size on natural-language pages without harming recall.</param>
/// <param name="SearchableFrontmatterKeys">UTF-8 frontmatter keys whose values are folded into each page's searchable text (e.g. <c>["author", "summary"]</c>). Empty for body-only indexing.</param>
/// <param name="ExtraStopwords">UTF-8 stopwords appended to the language defaults; tokens matching any of these are dropped from the index.</param>
/// <param name="Compression">Sibling-compression knob honored by the index writer.</param>
/// <param name="SectionPriorities">
/// UTF-8 comma-separated <c>prefix:weight</c> pairs (e.g. <c>"documentation/:80,api/:-200"</c>) that bias result ranking
/// when a URL contains a given prefix. Higher weights bubble matching pages up; negative weights demote them. Empty
/// disables section weighting and results sort by title relevance only. Surfaced via the
/// <c>nustreamdocs:search-section-priorities</c> meta tag for theme JS to read.
/// </param>
public readonly record struct SearchOptions(
    SearchFormat Format,
    PathSegment OutputSubdirectory,
    string Language,
    int MinTokenLength,
    byte[][] SearchableFrontmatterKeys,
    byte[][] ExtraStopwords,
    SearchCompression Compression,
    byte[] SectionPriorities)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static SearchOptions Default { get; } = new(
        Format: SearchFormat.Pagefind,
        OutputSubdirectory: "search",
        Language: "en",
        MinTokenLength: 3,
        SearchableFrontmatterKeys: [],
        ExtraStopwords: [],
        Compression: SearchCompression.Default,
        SectionPriorities: []);
}
