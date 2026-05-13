// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Configuration for <see cref="PagefindSearchPlugin"/>.</summary>
/// <param name="OutputSubdirectory">Site-relative directory used for engine bookkeeping (e.g. <c>search</c>). Real Pagefind output always lands at <c>&lt;site&gt;/pagefind/</c>.</param>
/// <param name="MinTokenLength">Documents whose extracted text is shorter than this are dropped from the index. Reduces noise from near-empty pages without harming recall.</param>
/// <param name="SearchableFrontmatterKeys">UTF-8 frontmatter keys whose values are folded into each page's searchable text (e.g. <c>["author", "summary"]</c>). Empty for body-only indexing.</param>
/// <param name="SectionPriorities">
/// UTF-8 comma-separated <c>prefix:weight</c> pairs (e.g. <c>"documentation/:80,api/:-200"</c>) that bias result ranking
/// when a URL contains a given prefix. Higher weights bubble matching pages up; negative weights demote them. Empty
/// disables section weighting and results sort by title relevance only. Surfaced via the
/// <c>nustreamdocs:search-section-priorities</c> meta tag for theme JS to read.
/// </param>
/// <param name="RunCli">
/// When true, invoke the bundled Pagefind binary against the rendered output to produce the WASM runtime + binary
/// inverted-index shards. Off skips invocation entirely — useful only for tests / dry runs.
/// </param>
/// <param name="BinaryPath">
/// Optional explicit path to the Pagefind binary; overrides the per-RID lookup under
/// <c>runtimes/&lt;rid&gt;/native/</c>. Empty means "auto-detect".
/// </param>
/// <param name="StrictBinaryRequired">
/// When true, missing binary or non-zero exit code throws instead of logging a warning. Use in CI publishes that
/// must produce real shards.
/// </param>
/// <param name="ExcludePathPrefixes">UTF-8 site-relative prefixes (e.g. <c>"api/"</c>) whose pages are skipped by the indexer.</param>
public readonly record struct PagefindOptions(
    PathSegment OutputSubdirectory,
    int MinTokenLength,
    byte[][] SearchableFrontmatterKeys,
    byte[] SectionPriorities,
    bool RunCli,
    FilePath BinaryPath,
    bool StrictBinaryRequired,
    byte[][] ExcludePathPrefixes)
{
    /// <summary>
    /// The default minimum token length.
    /// </summary>
    private const int DefaultTokenLength = 3;

    /// <summary>Gets the option set with all defaults populated.</summary>
    public static PagefindOptions Default { get; } = new(
        "search",
        DefaultTokenLength,
        [],
        [],
        true,
        default,
        false,
        []);
}
