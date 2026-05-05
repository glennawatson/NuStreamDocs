// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search;

/// <summary>Which on-disk index format the search plugin produces.</summary>
public enum SearchFormat
{
    /// <summary>
    /// Pagefind-compatible sharded index. Default for large corpora —
    /// only the shard touching a given search term loads on demand,
    /// so first-paint stays small even at 13K+ pages.
    /// </summary>
    /// <remarks>
    /// Output layout matches the Pagefind 1.x JSON contract: a
    /// <c>pagefind-entry.json</c> manifest plus per-page records. The
    /// runtime UI is supplied by Pagefind's <c>pagefind-ui.js</c>;
    /// themes can embed <c>&lt;div data-pagefind-search&gt;&lt;/div&gt;</c>.
    /// </remarks>
    Pagefind = 0,

    /// <summary>
    /// Lunr-compatible JSON document for compatibility with the
    /// upstream mkdocs-material bundled JS, which expects a
    /// <c>search_index.json</c> file with <c>config</c>, <c>docs</c>
    /// and <c>index</c> sections.
    /// </summary>
    Lunr
}
