// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// </remarks>
internal sealed class NavNode
{
    /// <summary>Length of the <c>.md</c> suffix converted to <c>.html</c>.</summary>
    private const int MarkdownExtensionLength = 3;

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
        RelativeUrl = ToPageUrl(relativePath);
        IndexUrl = ToPageUrl(indexPath);
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

    /// <summary>Gets the page URL derived from <see cref="RelativePath"/>.</summary>
    public string RelativeUrl { get; }

    /// <summary>Gets the page URL derived from <see cref="IndexPath"/>; empty when this node has no promoted index page.</summary>
    public string IndexUrl { get; }

    /// <summary>Gets the parent section in the nav tree; null for the root and synthetic leaves built outside the tree.</summary>
    public NavNode? Parent { get; private set; }

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

    /// <summary>Maps a source-relative markdown path to the rendered-page URL.</summary>
    /// <param name="path">Source-relative path.</param>
    /// <returns>Rendered-page URL.</returns>
    private static string ToPageUrl(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? $"{path.AsSpan(0, path.Length - MarkdownExtensionLength)}.html"
            : path;
}
