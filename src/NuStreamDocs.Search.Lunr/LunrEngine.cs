// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Lunr;

/// <summary>Lunr-format <see cref="ISearchEngine"/> implementation.</summary>
public sealed class LunrEngine : ISearchEngine
{
    /// <summary>UTF-8 Lunr language code emitted into the <c>config</c> block.</summary>
    private readonly byte[] _language;

    /// <summary>Extra stopwords emitted into the <c>config</c> block.</summary>
    private readonly byte[][] _extraStopwords;

    /// <summary>Initializes a new instance of the <see cref="LunrEngine"/> class.</summary>
    /// <param name="language">UTF-8 Lunr language code.</param>
    /// <param name="extraStopwords">UTF-8 stopwords surfaced to the runtime.</param>
    public LunrEngine(byte[] language, byte[][] extraStopwords)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(extraStopwords);
        _language = language;
        _extraStopwords = extraStopwords;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> FormatName => "lunr"u8;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> ManifestFileName => "/search_index.json"u8;

    /// <inheritdoc/>
    public FilePath Write(DirectoryPath searchRoot, SearchDocument[] documents)
    {
        var path = searchRoot.File("search_index.json");
        LunrIndexWriter.Write(path, _language, documents, _extraStopwords);
        return path;
    }
}
