// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Nav;

namespace NuStreamDocs.Config.DocFx;

/// <summary>
/// Reads docfx-style <c>toc.yml</c> files and converts them into <see cref="NavEntry"/> trees.
/// </summary>
/// <remarks>
/// Targets the narrow toc.yml subset docfx and the rxui website use:
/// <list type="bullet">
///   <item><description><c>- name: Title</c> + <c>href: foo.md</c> — leaf page.</description></item>
///   <item><description><c>- name: Title</c> + <c>href: subdir/toc.yml</c> — section loaded recursively.</description></item>
///   <item><description>
///     <c>- name: Title</c> + <c>href: subdir/</c> [+ <c>homepage: subdir/index.md</c>] —
///     section with optional landing page; sub-toc loaded from <c>subdir/toc.yml</c> if present.
///   </description></item>
///   <item><description><c>- name: Section</c> + <c>items:</c> + nested list — inline section without a sub-toc file.</description></item>
/// </list>
/// Out of scope: <c>uid:</c>-resolved cross-toc xrefs, <c>topicHref</c>, <c>tocHref</c> overrides — the rxui website doesn't use them and they aren't worth the parser complexity.
/// <para>
/// Hand-rolled line-based scanner — no YAML library dependency. Each toc.yml file is read once into
/// a stack-bounded buffer (or pooled rental for large files), parsed in a single forward pass with
/// the indent-tracking state machine, then UTF-8 strings flow through to <see cref="NavEntry"/>.
/// </para>
/// </remarks>
public static class DocFxTocReader
{
    /// <summary>Gets the default toc file name in each directory.</summary>
    public static string TocFileName => "toc.yml";

    /// <summary>Reads the docs-root <c>toc.yml</c> at <paramref name="rootDirectory"/> and returns the resolved nav tree.</summary>
    /// <param name="rootDirectory">Absolute path to the directory containing the root <c>toc.yml</c>.</param>
    /// <returns>The top-level entries; empty when no <c>toc.yml</c> is present.</returns>
    public static NavEntry[] ReadTree(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        var rootToc = Path.Combine(rootDirectory, TocFileName);
        return !File.Exists(rootToc) ? [] : ReadOneFile(rootDirectory, rootToc, currentDirectory: rootDirectory);
    }

    /// <summary>Reads <paramref name="tocFilePath"/> and resolves any nested toc references relative to <paramref name="currentDirectory"/>.</summary>
    /// <param name="rootDirectory">Site docs root (used to compute returned entry paths in forward-slash root-relative form).</param>
    /// <param name="tocFilePath">Absolute path of the toc.yml to read.</param>
    /// <param name="currentDirectory">Directory containing the toc.yml; relative refs resolve from here.</param>
    /// <returns>Decoded entries.</returns>
    private static NavEntry[] ReadOneFile(string rootDirectory, string tocFilePath, string currentDirectory)
    {
        var bytes = File.ReadAllBytes(tocFilePath);
        return ParseEntries(rootDirectory, currentDirectory, bytes);
    }

    /// <summary>Parses one toc.yml UTF-8 byte array into entries, recursively following <c>toc.yml</c> sub-includes.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Directory of the toc.yml being parsed.</param>
    /// <param name="utf8">UTF-8 bytes.</param>
    /// <returns>Decoded entries.</returns>
    private static NavEntry[] ParseEntries(string rootDirectory, string currentDirectory, ReadOnlySpan<byte> utf8)
    {
        TocLineParser lines = new(Utf8Bom.Strip(utf8));
        return ParseSequenceAt(rootDirectory, currentDirectory, ref lines, baseIndent: -1);
    }

    /// <summary>Parses a YAML block-sequence whose items live at indent &gt; <paramref name="baseIndent"/>.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Directory of the toc.yml being parsed.</param>
    /// <param name="lines">Forward line cursor into the toc bytes.</param>
    /// <param name="baseIndent">Indent floor; sequence ends when an item is at or below this column.</param>
    /// <returns>Decoded entries at this depth.</returns>
    private static NavEntry[] ParseSequenceAt(string rootDirectory, string currentDirectory, ref TocLineParser lines, int baseIndent)
    {
        var rented = ArrayPool<NavEntry>.Shared.Rent(8);
        var count = 0;
        try
        {
            while (lines.Peek(out var line) && line.IsSequenceItem && line.Indent > baseIndent)
            {
                var item = ParseOneItem(rootDirectory, currentDirectory, ref lines);
                if (count == rented.Length)
                {
                    var grown = ArrayPool<NavEntry>.Shared.Rent(rented.Length * 2);
                    Array.Copy(rented, grown, count);
                    ArrayPool<NavEntry>.Shared.Return(rented, clearArray: true);
                    rented = grown;
                }

                rented[count++] = item;
            }

            if (count is 0)
            {
                return [];
            }

            var result = new NavEntry[count];
            Array.Copy(rented, result, count);
            return result;
        }
        finally
        {
            ArrayPool<NavEntry>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <summary>Parses a single sequence item starting at the cursor; reads its <c>name</c>/<c>href</c>/<c>homepage</c>/<c>items</c> block.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Directory of the toc.yml.</param>
    /// <param name="lines">Cursor.</param>
    /// <returns>The decoded entry.</returns>
    private static NavEntry ParseOneItem(string rootDirectory, string currentDirectory, ref TocLineParser lines)
    {
        ItemFields fields = default;
        if (!lines.TryConsume(out var firstLine))
        {
            return default;
        }

        ApplyKey(firstLine, ref fields);
        var bodyIndent = firstLine.Indent + 2; // body keys live two columns past the `- ` marker.
        while (lines.Peek(out var next) && !next.IsSequenceItem && next.Indent >= bodyIndent)
        {
            if (next.HasItemsKey && bodyIndent <= next.Indent)
            {
                lines.TryConsume(out _);

                // mkdocs-style YAML lets items: children sit at the same column as the items key,
                // so the floor here is one less than the items column rather than equal to it.
                fields.InlineChildren = ParseSequenceAt(rootDirectory, currentDirectory, ref lines, next.Indent - 1);
                continue;
            }

            lines.TryConsume(out _);
            ApplyKey(next, ref fields);
        }

        return Materialize(rootDirectory, currentDirectory, fields);
    }

    /// <summary>Stamps a key/value pair from <paramref name="line"/> into <paramref name="fields"/>.</summary>
    /// <param name="line">Parsed line.</param>
    /// <param name="fields">Accumulating fields.</param>
    private static void ApplyKey(in TocLine line, ref ItemFields fields)
    {
        switch (line.KeyKind)
        {
            case TocKey.Name:
            {
                fields.Name = line.Value.ToArray();
                break;
            }

            case TocKey.Href:
            {
                fields.Href = line.Value.ToArray();
                break;
            }

            case TocKey.Homepage:
            {
                fields.Homepage = line.Value.ToArray();
                break;
            }
        }
    }

    /// <summary>Materializes a fully-collected item's fields into a <see cref="NavEntry"/>.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Directory of the toc.yml.</param>
    /// <param name="fields">Accumulated fields.</param>
    /// <returns>The decoded entry.</returns>
    private static NavEntry Materialize(string rootDirectory, string currentDirectory, ItemFields fields)
    {
        var name = fields.Name ?? [];
        var inlineChildren = fields.InlineChildren ?? [];
        if (inlineChildren.Length > 0)
        {
            // Inline section — `items:` provided, ignore href/homepage.
            return new(name, [], inlineChildren);
        }

        var href = fields.Href ?? [];
        return href.Length is 0 ? new(name, [], []) : MaterializeHref(rootDirectory, currentDirectory, name, href, fields.Homepage ?? []);
    }

    /// <summary>Routes an item with a populated <c>href</c> to the right resolver.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Directory of the toc.yml.</param>
    /// <param name="name">Title bytes.</param>
    /// <param name="href">Href bytes.</param>
    /// <param name="homepage">Homepage bytes (may be empty).</param>
    /// <returns>The decoded entry.</returns>
    private static NavEntry MaterializeHref(string rootDirectory, string currentDirectory, byte[] name, byte[] href, byte[] homepage)
    {
        if (IsAbsoluteUrl(href))
        {
            return new(name, href, []);
        }

        var hrefString = Encoding.UTF8.GetString(href);
        if (EndsWith(href, ".yml"u8) || EndsWith(href, ".yaml"u8))
        {
            return ResolveSubToc(rootDirectory, currentDirectory, name, hrefString);
        }

        if (EndsWithSlash(href))
        {
            return ResolveDirectoryRef(rootDirectory, currentDirectory, name, hrefString, homepage);
        }

        var rootRelative = ToRootRelative(rootDirectory, currentDirectory, hrefString);
        return new(name, Encoding.UTF8.GetBytes(rootRelative), []);
    }

    /// <summary>Loads the sub-toc at <paramref name="hrefString"/> and bundles its entries under a section named <paramref name="title"/>.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Directory of the parent toc.yml.</param>
    /// <param name="title">Section title (UTF-8).</param>
    /// <param name="hrefString">Decoded href value.</param>
    /// <returns>Decoded section entry.</returns>
    private static NavEntry ResolveSubToc(string rootDirectory, string currentDirectory, byte[] title, string hrefString)
    {
        var subTocPath = Path.GetFullPath(Path.Combine(currentDirectory, hrefString));
        if (!File.Exists(subTocPath))
        {
            return new(title, [], []);
        }

        var subDir = Path.GetDirectoryName(subTocPath) ?? currentDirectory;
        var children = ReadOneFile(rootDirectory, subTocPath, subDir);
        return new(title, [], children);
    }

    /// <summary>Resolves a <c>href: subdir/</c> reference: looks for a sub-toc.yml, otherwise treats the directory as a section with the optional homepage.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Directory of the parent toc.yml.</param>
    /// <param name="title">Section title.</param>
    /// <param name="hrefString">Decoded href value (trailing-slash directory ref).</param>
    /// <param name="homepage">Decoded homepage bytes (may be empty).</param>
    /// <returns>Section entry.</returns>
    private static NavEntry ResolveDirectoryRef(string rootDirectory, string currentDirectory, byte[] title, string hrefString, byte[] homepage)
    {
        var subDir = Path.GetFullPath(Path.Combine(currentDirectory, hrefString));
        var subToc = Path.Combine(subDir, TocFileName);
        var children = File.Exists(subToc)
            ? ReadOneFile(rootDirectory, subToc, subDir)
            : [];

        if (homepage.Length is 0)
        {
            return new(title, [], children);
        }

        var homepageString = Encoding.UTF8.GetString(homepage);
        var homepageRel = ToRootRelative(rootDirectory, currentDirectory, homepageString);
        return new(title, Encoding.UTF8.GetBytes(homepageRel), children);
    }

    /// <summary>Returns true when <paramref name="path"/> begins with <c>http://</c> or <c>https://</c>.</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <returns>True for absolute http(s) URLs.</returns>
    private static bool IsAbsoluteUrl(ReadOnlySpan<byte> path) =>
        path.StartsWith("http://"u8) || path.StartsWith("https://"u8);

    /// <summary>Returns true when <paramref name="path"/> ends with <paramref name="suffix"/> (case-insensitive ASCII).</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <param name="suffix">Suffix to match.</param>
    /// <returns>True on match.</returns>
    private static bool EndsWith(ReadOnlySpan<byte> path, ReadOnlySpan<byte> suffix)
    {
        if (path.Length < suffix.Length)
        {
            return false;
        }

        var tail = path[^suffix.Length..];
        for (var i = 0; i < suffix.Length; i++)
        {
            var lhs = tail[i] is >= (byte)'A' and <= (byte)'Z' ? (byte)(tail[i] | 0x20) : tail[i];
            if (lhs != suffix[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns true when <paramref name="path"/> ends with a forward or back slash.</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <returns>True for trailing-slash refs.</returns>
    private static bool EndsWithSlash(ReadOnlySpan<byte> path) =>
        path.Length > 0 && path[^1] is (byte)'/' or (byte)'\\';

    /// <summary>Resolves <paramref name="hrefString"/> against <paramref name="currentDirectory"/> and returns it as a root-relative forward-slash path.</summary>
    /// <param name="rootDirectory">Site docs root.</param>
    /// <param name="currentDirectory">Toc.yml directory.</param>
    /// <param name="hrefString">Decoded href.</param>
    /// <returns>Root-relative path, forward slashes.</returns>
    private static string ToRootRelative(string rootDirectory, string currentDirectory, string hrefString)
    {
        var absolute = Path.GetFullPath(Path.Combine(currentDirectory, hrefString));
        var rel = Path.GetRelativePath(rootDirectory, absolute);
        return rel.Replace('\\', '/');
    }

    /// <summary>Field accumulator for one in-flight item.</summary>
    private record struct ItemFields
    {
        /// <summary>Gets or sets the UTF-8 <c>name</c> bytes (display title).</summary>
        public byte[]? Name { get; set; }

        /// <summary>Gets or sets the UTF-8 <c>href</c> bytes (page, sub-toc, or directory ref).</summary>
        public byte[]? Href { get; set; }

        /// <summary>Gets or sets the UTF-8 <c>homepage</c> bytes (explicit landing page for a directory ref).</summary>
        public byte[]? Homepage { get; set; }

        /// <summary>Gets or sets the inline children when an <c>items:</c> sub-list was provided.</summary>
        public NavEntry[]? InlineChildren { get; set; }
    }
}
