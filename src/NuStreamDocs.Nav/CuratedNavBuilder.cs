// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// <summary>Maximum bytes peeked from the front of each leaf file when extracting the front-matter title.</summary>
    private const int FrontmatterPeekBytes = 1024;

    /// <summary>UTF-8 markdown extension bytes.</summary>
    private static readonly byte[] MarkdownExtensionBytes = ".md"u8.ToArray();

    /// <summary>Builds the nav tree from <paramref name="entries"/>.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="entries">Top-level curated entries.</param>
    /// <returns>Root <see cref="NavNode"/>.</returns>
    public static NavNode Build(string inputRoot, NavEntry[] entries) =>
        Build(inputRoot, entries, NullLogger.Instance);

    /// <summary>Builds the nav tree from <paramref name="entries"/> with a logger for orphan / missing-file diagnostics.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="entries">Top-level curated entries.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Root <see cref="NavNode"/>.</returns>
    public static NavNode Build(string inputRoot, NavEntry[] entries, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputRoot);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(logger);

        var children = new NavNode[entries.Length];
        var written = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            var built = BuildEntry(inputRoot, entries[i], logger);
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

        var root = new NavNode(string.Empty, string.Empty, isSection: true, children);
        root.AttachParents();
        return root;
    }

    /// <summary>Builds one entry into a <see cref="NavNode"/>; returns null when the entry is malformed.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Curated entry.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The built node or null.</returns>
    private static NavNode? BuildEntry(string inputRoot, in NavEntry entry, ILogger logger)
    {
        if (entry.IsSection)
        {
            return BuildSectionEntry(inputRoot, entry, logger);
        }

        if (entry.Path.Length is 0)
        {
            // Empty leaf: skip silently (callers can carry placeholder entries).
            return null;
        }

        return BuildLeafEntry(inputRoot, entry);
    }

    /// <summary>Builds a leaf page or external-link node.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Leaf entry.</param>
    /// <returns>Node.</returns>
    private static NavNode BuildLeafEntry(string inputRoot, in NavEntry entry)
    {
        var pathString = Encoding.UTF8.GetString(entry.Path);
        var title = ResolveTitle(inputRoot, entry, pathString);
        return new(title, pathString, isSection: false, []);
    }

    /// <summary>Builds a section node and its children.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Section entry.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Section node.</returns>
    private static NavNode BuildSectionEntry(string inputRoot, in NavEntry entry, ILogger logger)
    {
        var children = new NavNode[entry.Children.Length];
        var written = 0;
        for (var i = 0; i < entry.Children.Length; i++)
        {
            var built = BuildEntry(inputRoot, entry.Children[i], logger);
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

        var title = entry.Title.Length is 0 ? string.Empty : Encoding.UTF8.GetString(entry.Title);
        var indexPath = entry.Path.Length is 0 ? string.Empty : Encoding.UTF8.GetString(entry.Path);
        return new(title, indexPath, isSection: true, children, indexPath);
    }

    /// <summary>Resolves a leaf entry's display title — explicit, then front-matter <c>title:</c>, then file stem.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="entry">Leaf entry.</param>
    /// <param name="pathString">Decoded path.</param>
    /// <returns>Title string.</returns>
    private static string ResolveTitle(string inputRoot, in NavEntry entry, string pathString)
    {
        if (entry.Title.Length > 0)
        {
            return Encoding.UTF8.GetString(entry.Title);
        }

        if (IsAbsoluteUrl(entry.Path) || !PathLooksLikeMarkdown(entry.Path))
        {
            // External link or non-markdown — fall back to the path stem.
            return Path.GetFileNameWithoutExtension(pathString);
        }

        return TryReadFrontmatterTitle(inputRoot, pathString) ?? Path.GetFileNameWithoutExtension(pathString);
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

    /// <summary>Peeks the head of <paramref name="pathString"/>'s file and returns its front-matter <c>title:</c> when present.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <param name="pathString">Source-relative path.</param>
    /// <returns>Title string or null when absent / unreadable.</returns>
    private static string? TryReadFrontmatterTitle(string inputRoot, string pathString)
    {
        var absolute = Path.Combine(inputRoot, pathString);
        try
        {
            using var handle = File.OpenHandle(absolute);
            var size = (int)Math.Min(FrontmatterPeekBytes, RandomAccess.GetLength(handle));
            if (size <= 0)
            {
                return null;
            }

            Span<byte> buffer = stackalloc byte[FrontmatterPeekBytes];
            var read = RandomAccess.Read(handle, buffer[..size], 0);
            var scalar = FrontmatterValueExtractor.GetScalar(buffer[..read], "title"u8);
            if (scalar.IsEmpty)
            {
                return null;
            }

            return Encoding.UTF8.GetString(StripYamlQuotes(scalar));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Drops a single matching pair of leading/trailing single- or double-quote bytes from <paramref name="value"/>.</summary>
    /// <param name="value">UTF-8 candidate.</param>
    /// <returns>Unquoted slice or <paramref name="value"/> unchanged.</returns>
    private static ReadOnlySpan<byte> StripYamlQuotes(ReadOnlySpan<byte> value)
    {
        if (value.Length >= 2
            && (value[0] is (byte)'"' or (byte)'\'')
            && value[^1] == value[0])
        {
            return value[1..^1];
        }

        return value;
    }
}
