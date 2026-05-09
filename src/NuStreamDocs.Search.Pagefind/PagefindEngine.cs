// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Pagefind-format <see cref="ISearchEngine"/> implementation; the actual index is produced by the Pagefind CLI from rendered HTML.</summary>
public sealed class PagefindEngine : ISearchEngine
{
    /// <summary>Stateless singleton instance.</summary>
    public static readonly PagefindEngine Instance = new();

    /// <summary>Initializes a new instance of the <see cref="PagefindEngine"/> class.</summary>
    private PagefindEngine()
    {
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> FormatName => "pagefind"u8;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> ManifestFileName => default;

    /// <inheritdoc/>
    public FilePath Write(DirectoryPath searchRoot, SearchDocument[] documents)
    {
        _ = searchRoot;
        _ = documents;
        return default;
    }
}
