// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Nav;

/// <summary>
/// Builds a <see cref="NavNode"/> tree from a curated <see cref="NavEntry"/> array (mkdocs.yml
/// <c>nav:</c> or a docfx <c>toc.yml</c> reader). Falls back to <see cref="NavTreeBuilder"/> for
/// the auto-discovery case.
/// </summary>
/// <remarks>
/// Per-entry title resolution mirrors mkdocs / awesome-nav: explicit title from the entry first,
/// then the file's front-matter <c>title:</c>, then a <see cref="Path.GetFileNameWithoutExtension(string)"/>
/// fallback. UTF-8 stays the storage format end-to-end — the only string materialization is at the
/// file-system boundary (one decode per leaf to compose the absolute path for front-matter peek).
/// </remarks>
internal static class CuratedNavBuilder
{
    /// <summary>UTF-8 markdown extension bytes.</summary>
    private static readonly byte[] MarkdownExtensionBytes = [.. ".md"u8];

    /// <summary>Builds the nav tree from <paramref name="entries"/>.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="entries">Top-level curated entries.</param>
    /// <returns>Root <see cref="NavNode"/>.</returns>
    public static NavNode Build(DirectoryPath inputRoot, NavEntry[] entries) =>
        Build(inputRoot, entries, useDirectoryUrls: false, NullLogger.Instance);

    /// <summary>Builds the nav tree from <paramref name="entries"/> with an explicit served URL shape.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="entries">Top-level curated entries.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>Root <see cref="NavNode"/>.</returns>
    public static NavNode Build(DirectoryPath inputRoot, NavEntry[] entries, bool useDirectoryUrls) =>
        Build(inputRoot, entries, useDirectoryUrls, NullLogger.Instance);

    /// <summary>Builds the nav tree from <paramref name="entries"/> with a logger for orphan / missing-file diagnostics.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="entries">Top-level curated entries.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Root <see cref="NavNode"/>.</returns>
    public static NavNode Build(DirectoryPath inputRoot, NavEntry[] entries, ILogger logger)
        => Build(inputRoot, entries, useDirectoryUrls: false, logger);

    /// <summary>Builds the nav tree from <paramref name="entries"/> with a logger for orphan / missing-file diagnostics.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="entries">Top-level curated entries.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Root <see cref="NavNode"/>.</returns>
    public static NavNode Build(DirectoryPath inputRoot, NavEntry[] entries, bool useDirectoryUrls, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputRoot);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(logger);

        var children = new NavNode[entries.Length];
        var written = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            var built = BuildEntry(inputRoot, entries[i], useDirectoryUrls, logger);
            if (built is not null)
            {
                children[written++] = built;
            }
        }

        if (written != entries.Length)
        {
            var trimmed = new NavNode[written];
            Array.Copy(children, trimmed, written);
            children = trimmed;
        }

        NavNode root = new([], default, isSection: true, children, useDirectoryUrls);
        root.AttachParents();
        return root;
    }

    /// <summary>Builds one entry into a <see cref="NavNode"/>; returns null when the entry is malformed.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Curated entry.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The built node or null.</returns>
    private static NavNode? BuildEntry(DirectoryPath inputRoot, in NavEntry entry, bool useDirectoryUrls, ILogger logger)
    {
        if (entry.IsSection)
        {
            return BuildSectionEntry(inputRoot, entry, useDirectoryUrls, logger);
        }

        if (entry.Path.Length is 0)
        {
            // Empty leaf: skip silently (callers can carry placeholder entries).
            return null;
        }

        return BuildLeafEntry(inputRoot, entry, useDirectoryUrls);
    }

    /// <summary>Builds a leaf page or external-link node.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Leaf entry.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>Node.</returns>
    private static NavNode BuildLeafEntry(DirectoryPath inputRoot, in NavEntry entry, bool useDirectoryUrls)
    {
        FilePath path = Encoding.UTF8.GetString(entry.Path);
        var title = ResolveTitle(inputRoot, entry, path);
        return new(title, path, isSection: false, [], useDirectoryUrls);
    }

    /// <summary>Builds a section node and its children.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Section entry.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Section node.</returns>
    private static NavNode BuildSectionEntry(DirectoryPath inputRoot, in NavEntry entry, bool useDirectoryUrls, ILogger logger)
    {
        var children = new NavNode[entry.Children.Length];
        var written = 0;
        for (var i = 0; i < entry.Children.Length; i++)
        {
            var built = BuildEntry(inputRoot, entry.Children[i], useDirectoryUrls, logger);
            if (built is not null)
            {
                children[written++] = built;
            }
        }

        if (written != children.Length)
        {
            var trimmed = new NavNode[written];
            Array.Copy(children, trimmed, written);
            children = trimmed;
        }

        byte[] title = entry.Title.Length is 0 ? [] : [.. entry.Title];
        FilePath indexPath = entry.Path.Length is 0 ? null : Encoding.UTF8.GetString(entry.Path);
        return new(title, indexPath, isSection: true, children, indexPath, useDirectoryUrls);
    }

    /// <summary>Resolves a leaf entry's display title — explicit, then front-matter <c>title:</c>, then file stem.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Leaf entry.</param>
    /// <param name="path">Decoded path.</param>
    /// <returns>Title bytes.</returns>
    private static byte[] ResolveTitle(DirectoryPath inputRoot, in NavEntry entry, FilePath path)
    {
        if (entry.Title.Length > 0)
        {
            return [.. entry.Title];
        }

        if (IsAbsoluteUrl(entry.Path) || !PathLooksLikeMarkdown(entry.Path))
        {
            // External link or non-markdown — fall back to the path stem.
            return Encoding.UTF8.GetBytes(path.FileNameWithoutExtension);
        }

        return FrontmatterTitleReader.ReadBytes(inputRoot.File(path.Value)) ?? Encoding.UTF8.GetBytes(path.FileNameWithoutExtension);
    }

    /// <summary>Returns true when the UTF-8 path begins with <c>http://</c> or <c>https://</c>.</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <returns>True for absolute http(s) URLs.</returns>
    private static bool IsAbsoluteUrl(ReadOnlySpan<byte> path) =>
        path.StartsWith("http://"u8) || path.StartsWith("https://"u8);

    /// <summary>Returns true when the UTF-8 path ends with <c>.md</c> (case-insensitive ASCII).</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <returns>True when markdown-shaped.</returns>
    private static bool PathLooksLikeMarkdown(ReadOnlySpan<byte> path)
    {
        if (path.Length < MarkdownExtensionBytes.Length)
        {
            return false;
        }

        var tail = path[^MarkdownExtensionBytes.Length..];
        for (var i = 0; i < MarkdownExtensionBytes.Length; i++)
        {
            var lhs = tail[i] is >= (byte)'A' and <= (byte)'Z' ? (byte)(tail[i] | 0x20) : tail[i];
            if (lhs != MarkdownExtensionBytes[i])
            {
                return false;
            }
        }

        return true;
    }
}
