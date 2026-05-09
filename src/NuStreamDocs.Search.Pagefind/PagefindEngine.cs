// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Pagefind-format <see cref="ISearchEngine"/> implementation — CLI-only.</summary>
/// <remarks>
/// Real Pagefind reads the rendered HTML directly to build its WASM-backed binary inverted-index
/// shards; the engine itself doesn't write any manifest at the base-pipeline stage. The CLI
/// invocation runs from <see cref="PagefindSearchPlugin"/>'s
/// <see cref="SearchPluginBase.OnIndexWrittenAsync"/> override, after the document scan but before
/// the build's static-asset emission, and produces <c>&lt;site&gt;/pagefind/</c> with the loader,
/// WASM, and shard files. Hence both <see cref="ManifestFileName"/> and <see cref="Write"/>'s
/// return value are intentionally empty.
/// </remarks>
public sealed class PagefindEngine : ISearchEngine
{
    /// <summary>Singleton — the engine is stateless; per-build configuration lives on the plugin's <see cref="PagefindOptions"/>.</summary>
    public static readonly PagefindEngine Instance = new();

    /// <summary>Initializes a new instance of the <see cref="PagefindEngine"/> class.</summary>
    private PagefindEngine()
    {
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> FormatName => "pagefind"u8;

    /// <inheritdoc/>
    /// <remarks>
    /// Empty — the Pagefind theme glue imports <c>/pagefind/pagefind.js</c> directly via the
    /// engine's head-extras, not through the universal <c>nustreamdocs:search-index</c> meta tag.
    /// </remarks>
    public ReadOnlySpan<byte> ManifestFileName => default;

    /// <inheritdoc/>
    /// <remarks>
    /// No-op — the actual index is produced by the Pagefind CLI from the rendered HTML, not from
    /// <see cref="SearchDocument"/>s. Returns an empty <see cref="FilePath"/> so the base
    /// pipeline knows there's no manifest to point at.
    /// </remarks>
    public FilePath Write(DirectoryPath searchRoot, SearchDocument[] documents)
    {
        _ = searchRoot;
        _ = documents;
        return default;
    }
}
