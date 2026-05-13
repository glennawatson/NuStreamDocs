// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Lunr;

/// <summary>Configuration for <see cref="LunrSearchPlugin"/>.</summary>
/// <param name="OutputSubdirectory">Site-relative directory the index files are written under (e.g. <c>search</c>).</param>
/// <param name="Language">UTF-8 stop-word + stemmer language code emitted in the Lunr <c>config</c> block; empty for English default.</param>
/// <param name="MinTokenLength">Documents whose extracted text is shorter than this are dropped from the index.</param>
/// <param name="SearchableFrontmatterKeys">UTF-8 frontmatter keys whose values are folded into each page's searchable text. Empty for body-only indexing.</param>
/// <param name="ExtraStopwords">UTF-8 stopwords appended to the language defaults.</param>
/// <param name="Compression">Sibling-compression knob honored after the manifest is written.</param>
/// <param name="SectionPriorities">UTF-8 comma-separated <c>prefix:weight</c> pairs that bias result ranking by URL prefix; empty disables section weighting.</param>
public readonly record struct LunrOptions(
    PathSegment OutputSubdirectory,
    byte[] Language,
    int MinTokenLength,
    byte[][] SearchableFrontmatterKeys,
    byte[][] ExtraStopwords,
    SearchCompression Compression,
    byte[] SectionPriorities)
{
    /// <summary>
    /// The default minimum token length.
    /// </summary>
    private const int DefaultTokenLength = 3;

    /// <summary>Gets the option set with all defaults populated.</summary>
    public static LunrOptions Default { get; } = new(
        "search",
        [.. "en"u8],
        DefaultTokenLength,
        [],
        [],
        SearchCompression.Default,
        []);
}
