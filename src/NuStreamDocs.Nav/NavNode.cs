// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Links;

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
/// <see cref="RelativePath"/> / <see cref="IndexPath"/> are <see cref="FilePath"/>-typed —
/// the source <c>.md</c> path used for filesystem lookups and Path.* helpers. The per-emit
/// <see cref="Title"/> / <see cref="RelativeUrlBytes"/> / <see cref="IndexUrlBytes"/> hold the
/// rendered <c>.html</c> URL bytes pre-encoded to UTF-8 once at construction so the renderer's
/// per-page emit loop never re-encodes them.
/// </para>
/// </remarks>
internal sealed class NavNode
{
    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class from already-encoded title bytes.</summary>
    /// <param name="title">Pre-encoded UTF-8 title bytes; ownership transfers to the node.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    /// <param name="indexPath">Source-relative path of the section's promoted index page (e.g. <c>guide/index.md</c>); empty when the section has no index landing page.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    public NavNode(byte[] title, FilePath relativePath, bool isSection, NavNode[] children, FilePath indexPath, bool useDirectoryUrls)
    {
        Title = title;
        RelativePath = relativePath;
        IsSection = isSection;
        Children = children;
        IndexPath = indexPath;
        RelativeUrlBytes = ServedUrlBytes.FromPath(relativePath, useDirectoryUrls);
        IndexUrlBytes = ServedUrlBytes.FromPath(indexPath, useDirectoryUrls);
    }

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class.</summary>
    /// <param name="title">Display title.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    /// <param name="indexPath">Source-relative path of the section's promoted index page (e.g. <c>guide/index.md</c>); empty when the section has no index landing page.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    public NavNode(string title, FilePath relativePath, bool isSection, NavNode[] children, FilePath indexPath, bool useDirectoryUrls)
        : this(string.IsNullOrEmpty(title) ? [] : Encoding.UTF8.GetBytes(title), relativePath, isSection, children, indexPath, useDirectoryUrls)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class.</summary>
    /// <param name="title">Display title.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    /// <param name="indexPath">Source-relative path of the section's promoted index page (e.g. <c>guide/index.md</c>); empty when the section has no index landing page.</param>
    public NavNode(string title, FilePath relativePath, bool isSection, NavNode[] children, FilePath indexPath)
        : this(title, relativePath, isSection, children, indexPath, useDirectoryUrls: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class without a promoted section index page.</summary>
    /// <param name="title">Display title.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    public NavNode(string title, FilePath relativePath, bool isSection, NavNode[] children, bool useDirectoryUrls)
        : this(title, relativePath, isSection, children, default, useDirectoryUrls)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class without a promoted section index page.</summary>
    /// <param name="title">Display title.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    public NavNode(string title, FilePath relativePath, bool isSection, NavNode[] children)
        : this(title, relativePath, isSection, children, default, useDirectoryUrls: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class from already-encoded title bytes.</summary>
    /// <param name="title">Pre-encoded UTF-8 title bytes; ownership transfers to the node.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    public NavNode(byte[] title, FilePath relativePath, bool isSection, NavNode[] children, bool useDirectoryUrls)
        : this(title, relativePath, isSection, children, default, useDirectoryUrls)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavNode"/> class from already-encoded title bytes.</summary>
    /// <param name="title">Pre-encoded UTF-8 title bytes; ownership transfers to the node.</param>
    /// <param name="relativePath">Source-relative path (file) or directory path (section).</param>
    /// <param name="isSection">True when this node is a section rather than a page.</param>
    /// <param name="children">Pre-sized child array; empty for leaf pages.</param>
    public NavNode(byte[] title, FilePath relativePath, bool isSection, NavNode[] children)
        : this(title, relativePath, isSection, children, useDirectoryUrls: false)
    {
    }

    /// <summary>Gets the UTF-8 display title bytes; encoded once at construction so renderers never transcode per emit.</summary>
    public byte[] Title { get; }

    /// <summary>Gets the source-relative path of the underlying file (or section directory).</summary>
    public FilePath RelativePath { get; }

    /// <summary>Gets a value indicating whether this node represents a section (directory) rather than a page.</summary>
    public bool IsSection { get; }

    /// <summary>Gets the child nodes; empty array for leaves.</summary>
    public NavNode[] Children { get; }

    /// <summary>Gets the source-relative path of the section's promoted index page; empty when this is a leaf or the section has no <c>index.md</c>.</summary>
    public FilePath IndexPath { get; }

    /// <summary>Gets the parent section in the nav tree; null for the root and synthetic leaves built outside the tree.</summary>
    public NavNode? Parent { get; private set; }

    /// <summary>Gets the UTF-8 page URL bytes derived from <see cref="RelativePath"/> (with the <c>.md</c> suffix swapped for <c>.html</c>).</summary>
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
}
