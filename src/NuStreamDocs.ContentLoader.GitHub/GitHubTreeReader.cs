// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.GitHub;

/// <summary>Reads a GitHub recursive git-tree response into the list of Markdown files to pull, paired with their local routes.</summary>
internal static class GitHubTreeReader
{
    /// <summary>Extracts the Markdown blobs under <paramref name="sourcePath"/> from a tree response.</summary>
    /// <param name="treeJson">UTF-8 JSON from the git-trees API.</param>
    /// <param name="repo">Repository reference (used to build raw-content URLs).</param>
    /// <param name="sourcePath">Repository subdirectory to include (empty = whole repo).</param>
    /// <param name="routePrefix">Local subdirectory the files are mounted under (empty = repo-relative).</param>
    /// <returns>One entry per Markdown file found.</returns>
    /// <exception cref="ContentLoaderException">When the response is not valid JSON.</exception>
    public static RawDocumentEntry[] Read(byte[] treeJson, in GitHubRepoRef repo, ReadOnlySpan<byte> sourcePath, ReadOnlySpan<byte> routePrefix)
    {
        var sourcePrefix = sourcePath.IsEmpty ? string.Empty : Encoding.UTF8.GetString(sourcePath).TrimEnd('/') + "/";
        var routeBase = routePrefix.IsEmpty ? string.Empty : Encoding.UTF8.GetString(routePrefix).TrimEnd('/');

        using var document = ParseOrThrow(treeJson);
        if (!document.RootElement.TryGetProperty("tree"u8, out var tree) || tree.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<RawDocumentEntry> entries = [];

        // foreach over JsonElement.ArrayEnumerator — a struct enumerator with no indexed alternative.
        foreach (var node in tree.EnumerateArray())
        {
            if (TryReadEntry(node, in repo, sourcePrefix, routeBase, out var entry))
            {
                entries.Add(entry);
            }
        }

        return [.. entries];
    }

    /// <summary>Parses <paramref name="json"/>, wrapping syntax errors in a <see cref="ContentLoaderException"/>.</summary>
    /// <param name="json">UTF-8 JSON bytes.</param>
    /// <returns>The parsed document.</returns>
    private static JsonDocument ParseOrThrow(byte[] json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ContentLoaderException("GitHub tree response is not valid JSON.", ex);
        }
    }

    /// <summary>Reads one tree node into an entry when it is a Markdown blob under the source path.</summary>
    /// <param name="node">A tree-array element.</param>
    /// <param name="repo">Repository reference.</param>
    /// <param name="sourcePrefix">Source path with a trailing slash, or empty.</param>
    /// <param name="routeBase">Local route prefix without a trailing slash, or empty.</param>
    /// <param name="entry">On success, the built entry.</param>
    /// <returns><see langword="true"/> when the node yielded an entry.</returns>
    private static bool TryReadEntry(JsonElement node, in GitHubRepoRef repo, string sourcePrefix, string routeBase, out RawDocumentEntry entry)
    {
        entry = default;
        if (!IsMarkdownBlob(node, out var path))
        {
            return false;
        }

        if (sourcePrefix.Length > 0 && !path.StartsWith(sourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var relative = sourcePrefix.Length > 0 ? path[sourcePrefix.Length..] : path;
        var route = routeBase.Length > 0 ? routeBase + "/" + relative : relative;
        entry = new((UrlPath)GitHubUrls.RawFileUrl(in repo, path), new FilePath(route));
        return true;
    }

    /// <summary>True when a tree node is a Markdown <c>blob</c>, yielding its repository-relative path.</summary>
    /// <param name="node">A tree-array element.</param>
    /// <param name="path">On return, the node's path (empty when the node is not a usable blob).</param>
    /// <returns><see langword="true"/> for a Markdown blob.</returns>
    private static bool IsMarkdownBlob(JsonElement node, out string path)
    {
        path = string.Empty;
        if (node.ValueKind != JsonValueKind.Object
            || !node.TryGetProperty("type"u8, out var type)
            || !type.ValueEquals("blob"u8)
            || !node.TryGetProperty("path"u8, out var pathElement)
            || pathElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        path = pathElement.GetString() ?? string.Empty;
        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }
}
