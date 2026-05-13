// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.GitHub;

/// <summary>Streams a GitHub recursive git-tree response into the Markdown files to pull, byte-first, with a forward-only <see cref="Utf8JsonReader"/> — never the whole document model.</summary>
internal static class GitHubTreeReader
{
    /// <summary>Byte length of the <c>.md</c> suffix.</summary>
    private const int MarkdownSuffixLength = 3;

    /// <summary>Extracts the Markdown blobs under <paramref name="sourcePath"/> from a tree response.</summary>
    /// <param name="treeJson">UTF-8 JSON from the git-trees API.</param>
    /// <param name="repo">Repository reference (used to build raw-content URLs).</param>
    /// <param name="sourcePath">Repository subdirectory to include (empty = whole repo).</param>
    /// <param name="routePrefix">Local subdirectory the files are mounted under (empty = repo-relative).</param>
    /// <returns>One entry per Markdown file found.</returns>
    /// <exception cref="ContentLoaderException">When the response is not valid JSON.</exception>
    public static RawDocumentEntry[] Read(
        byte[] treeJson,
        in GitHubRepoRef repo,
        ReadOnlySpan<byte> sourcePath,
        ReadOnlySpan<byte> routePrefix)
    {
        ArgumentNullException.ThrowIfNull(treeJson);
        var sourcePrefix = NormalizeSourcePrefix(sourcePath);
        var routeBase = NormalizeRouteBase(routePrefix);

        List<RawDocumentEntry> entries = [];
        try
        {
            Utf8JsonReader reader = new(treeJson);
            if (!AdvanceToTreeArray(ref reader))
            {
                return [];
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                var (path, isBlob) = ReadNode(ref reader);
                if (TryBuildEntry(path, isBlob, in repo, sourcePrefix, routeBase, out var entry))
                {
                    entries.Add(entry);
                }
            }
        }
        catch (JsonException ex)
        {
            throw new ContentLoaderException("GitHub tree response is not valid JSON.", ex);
        }

        return [.. entries];
    }

    /// <summary>Advances <paramref name="reader"/> to the start of the root object's <c>tree</c> array.</summary>
    /// <param name="reader">Reader at the document start.</param>
    /// <returns><see langword="true"/> when a <c>tree</c> array was found.</returns>
    private static bool AdvanceToTreeArray(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isTree = reader.ValueTextEquals("tree"u8);
            if (!reader.Read())
            {
                return false;
            }

            if (isTree)
            {
                return reader.TokenType == JsonTokenType.StartArray;
            }

            reader.Skip();
        }

        return false;
    }

    /// <summary>Reads one tree-node object's <c>path</c> and <c>type</c> properties; the reader ends on the node's <c>}</c>.</summary>
    /// <param name="reader">Reader at the node's <c>{</c>.</param>
    /// <returns>The node's path bytes (or null) and whether it is a <c>blob</c>.</returns>
    private static (byte[]? Path, bool IsBlob) ReadNode(ref Utf8JsonReader reader)
    {
        byte[]? path = null;
        var isBlob = false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isPath = reader.ValueTextEquals("path"u8);
            var isType = reader.ValueTextEquals("type"u8);
            if (!reader.Read())
            {
                break;
            }

            if (isPath && reader.TokenType == JsonTokenType.String)
            {
                path = ReadStringBytes(ref reader);
            }
            else if (isType)
            {
                isBlob = reader.TokenType == JsonTokenType.String && reader.ValueTextEquals("blob"u8);
            }
            else
            {
                reader.Skip();
            }
        }

        return (path, isBlob);
    }

    /// <summary>Copies the unescaped UTF-8 bytes of the current string token.</summary>
    /// <param name="reader">Reader positioned on a string token.</param>
    /// <returns>The value bytes.</returns>
    private static byte[] ReadStringBytes(ref Utf8JsonReader reader)
    {
        var destination = new byte[reader.ValueSpan.Length];
        var written = reader.CopyString(destination);
        return written == destination.Length ? destination : destination[..written];
    }

    /// <summary>Builds the entry for a tree node when it is a Markdown blob under the source path.</summary>
    /// <param name="path">The node's path bytes, or null.</param>
    /// <param name="isBlob">Whether the node is a <c>blob</c>.</param>
    /// <param name="repo">Repository reference.</param>
    /// <param name="sourcePrefix">Source path with a trailing slash, or empty.</param>
    /// <param name="routeBase">Local route prefix without a trailing slash, or empty.</param>
    /// <param name="entry">On success, the built entry.</param>
    /// <returns><see langword="true"/> when the node yielded an entry.</returns>
    private static bool TryBuildEntry(
        byte[]? path,
        bool isBlob,
        in GitHubRepoRef repo,
        ReadOnlySpan<byte> sourcePrefix,
        ReadOnlySpan<byte> routeBase,
        out RawDocumentEntry entry)
    {
        entry = default;
        if (!isBlob || path is null or [] || !EndsWithMarkdown(path))
        {
            return false;
        }

        ReadOnlySpan<byte> resolved = path;
        if (sourcePrefix.Length > 0 && !resolved.StartsWith(sourcePrefix))
        {
            return false;
        }

        var relative = sourcePrefix.Length > 0 ? resolved[sourcePrefix.Length..] : resolved;
        var route = routeBase.Length > 0 ? [.. routeBase, (byte)'/', .. relative] : relative.ToArray();
        entry = new(GitHubUrls.RawFileUrl(in repo, resolved), new(Encoding.UTF8.GetString(route)));
        return true;
    }

    /// <summary>True when <paramref name="path"/> ends with the ASCII suffix <c>.md</c> (case-insensitive).</summary>
    /// <param name="path">Repository-relative path bytes.</param>
    /// <returns><see langword="true"/> for a Markdown file.</returns>
    private static bool EndsWithMarkdown(ReadOnlySpan<byte> path) =>
        path.Length >= MarkdownSuffixLength &&
        AsciiByteHelpers.EqualsIgnoreAsciiCase(path[^MarkdownSuffixLength..], ".md"u8);

    /// <summary>Normalizes the source-path prefix to <c>{path}/</c> bytes (empty stays empty).</summary>
    /// <param name="sourcePath">Configured source path.</param>
    /// <returns>The prefix bytes.</returns>
    private static byte[] NormalizeSourcePrefix(ReadOnlySpan<byte> sourcePath)
    {
        if (sourcePath.IsEmpty)
        {
            return [];
        }

        var trimmed = sourcePath[^1] == (byte)'/' ? sourcePath[..^1] : sourcePath;
        return [.. trimmed, (byte)'/'];
    }

    /// <summary>Normalizes the route-prefix to bytes without a trailing slash (empty stays empty).</summary>
    /// <param name="routePrefix">Configured route prefix.</param>
    /// <returns>The prefix bytes.</returns>
    private static byte[] NormalizeRouteBase(ReadOnlySpan<byte> routePrefix)
    {
        if (routePrefix.IsEmpty)
        {
            return [];
        }

        return (routePrefix[^1] == (byte)'/' ? routePrefix[..^1] : routePrefix).ToArray();
    }
}
