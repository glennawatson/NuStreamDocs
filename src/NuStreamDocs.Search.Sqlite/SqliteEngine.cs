// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>SQLite/FTS5 <see cref="ISearchEngine"/> implementation — writes a single <c>search.db</c>.</summary>
public sealed class SqliteEngine : ISearchEngine
{
    /// <summary>UTF-8 root-relative URL prefixes whose pages are dropped from the index.</summary>
    private readonly byte[][] _excludePathPrefixes;

    /// <summary>Whether the full body text is stored in the index.</summary>
    private readonly bool _indexFullBody;

    /// <summary>Initializes a new instance of the <see cref="SqliteEngine"/> class.</summary>
    /// <param name="excludePathPrefixes">UTF-8 root-relative URL prefixes whose pages are dropped from the index; empty indexes every page.</param>
    /// <param name="indexFullBody">When true the full body text is stored; when false only a short leading excerpt.</param>
    public SqliteEngine(byte[][] excludePathPrefixes, bool indexFullBody)
    {
        _excludePathPrefixes = excludePathPrefixes;
        _indexFullBody = indexFullBody;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> FormatName => "sqlite"u8;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> ManifestFileName => "/search.db"u8;

    /// <inheritdoc/>
    public FilePath Write(DirectoryPath searchRoot, SearchDocument[] documents)
    {
        var path = searchRoot.File("search.db");
        var kept = Filter(documents, _excludePathPrefixes);
        SqliteIndexWriter.Write(path, kept, _indexFullBody);
        return path;
    }

    /// <summary>Returns the subset of <paramref name="documents"/> whose URL does not start with any excluded prefix.</summary>
    /// <param name="documents">Source documents.</param>
    /// <param name="excludePrefixes">UTF-8 root-relative URL prefixes to drop.</param>
    /// <returns>The kept documents, or the original array when nothing is excluded.</returns>
    private static SearchDocument[] Filter(SearchDocument[] documents, byte[][] excludePrefixes)
    {
        if (excludePrefixes.Length == 0)
        {
            return documents;
        }

        List<SearchDocument> kept = new(documents.Length);
        for (var i = 0; i < documents.Length; i++)
        {
            if (!StartsWithAny(documents[i].RelativeUrl, excludePrefixes))
            {
                kept.Add(documents[i]);
            }
        }

        return [.. kept];
    }

    /// <summary>Tests whether <paramref name="url"/> (root-relative, leading <c>/</c>) starts with any of <paramref name="prefixes"/>.</summary>
    /// <param name="url">Root-relative URL bytes.</param>
    /// <param name="prefixes">UTF-8 prefixes written without a leading slash (e.g. <c>api/</c>).</param>
    /// <returns>True when a prefix matches.</returns>
    private static bool StartsWithAny(byte[] url, byte[][] prefixes)
    {
        // RelativeUrl is root-relative with a leading '/'; prefixes are written like "api/", so compare against url[1..].
        var rel = url.Length > 0 && url[0] == (byte)'/' ? url.AsSpan(1) : url.AsSpan();
        for (var i = 0; i < prefixes.Length; i++)
        {
            if (rel.StartsWith(prefixes[i]))
            {
                return true;
            }
        }

        return false;
    }
}
