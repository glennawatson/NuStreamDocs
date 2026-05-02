// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Nav;

/// <summary>
/// One node in the rendered navigation tree.
/// </summary>
/// <remarks>
/// Reference-typed because nav trees are deeply shared (cross-page
/// breadcrumbs, sidebar, search-scoping) and equality is identity, not
/// structural. <see cref="Children"/> is a plain <c>NavNode[]</c>: each
/// builder pass right-sizes the array up front so we never pay the
/// <c>List&lt;T&gt;</c>-doubling overhead on large projects.
/// <para>
/// <see cref="RelativePath"/> and <see cref="IndexPath"/> stay
/// <see cref="string"/> because they cross to <see cref="System.IO"/>
/// (<see cref="Path.Combine(string, string)"/>, <see cref="File"/>);
/// the per-emit fields (<see cref="TitleBytes"/>,
/// <see cref="RelativeUrlBytes"/>, <see cref="IndexUrlBytes"/>) are
/// pre-encoded to UTF-8 once at construction so the renderer's per-page
/// emit loop never re-encodes them.
/// </para>
/// </remarks>
internal sealed class NavNode
{
    /// <summary>Length of the <c>.md</c> suffix converted to <c>.html</c>.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>HTML extension bytes appended when swapping a markdown extension on the rendered URL.</summary>
    private static readonly byte[] HtmlExtension = ".html"u8.ToArray();

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class.</summary>
    /// <param name="title">Display title.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    /// <param name="indexPath">Source-relative path of the section's promoted index page (e.g. <c>guide/index.md</c>); empty when the section has no index landing page.</param>
    public NavNode(string title, string relativePath, bool isSection, NavNode[] children, string indexPath)
    {
        Title = title;
        RelativePath = relativePath;
        IsSection = isSection;
        Children = children;
        IndexPath = indexPath;
        TitleBytes = Encoding.UTF8.GetBytes(title);
        RelativeUrlBytes = ToPageUrlBytes(relativePath);
        IndexUrlBytes = ToPageUrlBytes(indexPath);
    }

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class without a promoted section index page.</summary>
    /// <param name="title">Display title.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    public NavNode(string title, string relativePath, bool isSection, NavNode[] children)
        : this(title, relativePath, isSection, children, string.Empty)
    {
    }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the source-relative path.</summary>
    public string RelativePath { get; }

    /// <summary>Gets a value indicating whether this node represents a section (directory) rather than a page.</summary>
    public bool IsSection { get; }

    /// <summary>Gets the child nodes; empty array for leaves.</summary>
    public NavNode[] Children { get; }

    /// <summary>Gets the source-relative path of the section's promoted index page; empty when this is a leaf or the section has no <c>index.md</c>.</summary>
    public string IndexPath { get; }

    /// <summary>Gets the parent section in the nav tree; null for the root and synthetic leaves built outside the tree.</summary>
    public NavNode? Parent { get; private set; }

    /// <summary>Gets the UTF-8 form of <see cref="Title"/>; pre-encoded once so the renderer never transcodes per emit.</summary>
    public byte[] TitleBytes { get; }

    /// <summary>Gets the UTF-8 page URL bytes derived from <see cref="RelativePath"/>.</summary>
    public byte[] RelativeUrlBytes { get; }

    /// <summary>Gets the UTF-8 page URL bytes derived from <see cref="IndexPath"/>; empty when this node has no promoted index page.</summary>
    public byte[] IndexUrlBytes { get; }

    /// <summary>Attaches <see cref="Parent"/> links for this node's subtree after construction.</summary>
    internal void AttachParents()
    {
        for (var i = 0; i < Children.Length; i++)
        {
            var child = Children[i];
            child.Parent = this;
            child.AttachParents();
        }
    }

    /// <summary>Maps a source-relative markdown path to the rendered-page URL bytes.</summary>
    /// <param name="path">Source-relative path.</param>
    /// <returns>UTF-8 bytes of the rendered-page URL; empty for an empty input.</returns>
    private static byte[] ToPageUrlBytes(string path)
    {
        if (path.Length is 0)
        {
            return [];
        }

        var stripMd = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        if (!stripMd)
        {
            return Encoding.UTF8.GetBytes(path);
        }

        var trimmed = path.AsSpan(0, path.Length - MarkdownExtensionLength);
        var byteCount = Encoding.UTF8.GetByteCount(trimmed);
        var output = new byte[byteCount + HtmlExtension.Length];
        var written = Encoding.UTF8.GetBytes(trimmed, output);
        HtmlExtension.CopyTo(output.AsSpan(written));
        return output;
    }
}
