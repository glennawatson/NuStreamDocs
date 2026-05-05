// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>
/// Construction helpers for <see cref="NavEntry"/>. Internally <see cref="NavEntry"/> stores its
/// title and path as UTF-8 byte arrays (encode-once at construction); these factories expose
/// matching string and byte-form overloads so callers can pick whichever is cheapest at the call
/// site without paying re-encoding round trips.
/// </summary>
public static class NavEntryFactory
{
    /// <summary>Diagnostic for a missing-children section construction.</summary>
    private const string EmptyChildrenError = "Sections must have at least one child entry.";

    /// <summary>Builds a leaf page entry from string inputs.</summary>
    /// <param name="title">Display title; pass <see cref="string.Empty"/> to derive from the file at render time.</param>
    /// <param name="path">Source-relative markdown path (forward slashes) or absolute URL.</param>
    /// <returns>A leaf <see cref="NavEntry"/>.</returns>
    public static NavEntry Leaf(ApiCompatString title, FilePath path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path.Value);
        return new(EncodeUtf8(title), EncodeUtf8(path), []);
    }

    /// <summary>Builds a leaf entry from raw UTF-8 bytes (zero-encode path).</summary>
    /// <param name="title">UTF-8 title bytes; empty to derive from the file at render time.</param>
    /// <param name="path">UTF-8 source-relative path bytes or absolute URL.</param>
    /// <returns>A leaf <see cref="NavEntry"/>.</returns>
    public static NavEntry Leaf(byte[] title, byte[] path)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(path);
        return path.Length is 0
            ? throw new ArgumentException("Path bytes must be non-empty for a leaf entry.", nameof(path))
            : new(title, path, []);
    }

    /// <summary>Builds a leaf entry from byte spans; allocates two arrays.</summary>
    /// <param name="title">UTF-8 title bytes; empty span to derive at render time.</param>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <returns>A leaf <see cref="NavEntry"/>.</returns>
    public static NavEntry Leaf(ReadOnlySpan<byte> title, ReadOnlySpan<byte> path) =>
        path.IsEmpty
            ? throw new ArgumentException("Path bytes must be non-empty for a leaf entry.", nameof(path))
            : new(title.ToArray(), path.ToArray(), []);

    /// <summary>Builds a pure section (no landing page) from a string title.</summary>
    /// <param name="title">Section display title.</param>
    /// <param name="children">Children; must be non-empty.</param>
    /// <returns>A section <see cref="NavEntry"/>.</returns>
    public static NavEntry Section(ApiCompatString title, NavEntry[] children)
    {
        ArgumentException.ThrowIfNullOrEmpty(title.Value);
        ArgumentNullException.ThrowIfNull(children);
        return children.Length is 0
            ? throw new ArgumentException(EmptyChildrenError, nameof(children))
            : new(EncodeUtf8(title), [], children);
    }

    /// <summary>Builds a pure section from UTF-8 title bytes.</summary>
    /// <param name="title">UTF-8 title bytes; must be non-empty.</param>
    /// <param name="children">Children; must be non-empty.</param>
    /// <returns>A section <see cref="NavEntry"/>.</returns>
    public static NavEntry Section(byte[] title, NavEntry[] children)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(children);
        if (title.Length is 0)
        {
            throw new ArgumentException("Title bytes must be non-empty for a section.", nameof(title));
        }

        return children.Length is 0 ? throw new ArgumentException(EmptyChildrenError, nameof(children)) : new(title, [], children);
    }

    /// <summary>Builds a section that names a landing page (e.g. <c>guide/index.md</c>) from string inputs.</summary>
    /// <param name="title">Section display title.</param>
    /// <param name="indexPath">Source-relative landing-page path.</param>
    /// <param name="children">Children; must be non-empty.</param>
    /// <returns>A section <see cref="NavEntry"/>.</returns>
    public static NavEntry SectionWithIndex(ApiCompatString title, FilePath indexPath, NavEntry[] children)
    {
        ArgumentException.ThrowIfNullOrEmpty(title.Value);
        ArgumentException.ThrowIfNullOrEmpty(indexPath.Value);
        ArgumentNullException.ThrowIfNull(children);
        return children.Length is 0
            ? throw new ArgumentException(EmptyChildrenError, nameof(children))
            : new(EncodeUtf8(title), EncodeUtf8(indexPath), children);
    }

    /// <summary>Builds a section with a landing page from UTF-8 title and path bytes.</summary>
    /// <param name="title">UTF-8 title bytes; must be non-empty.</param>
    /// <param name="indexPath">UTF-8 landing-page path bytes; must be non-empty.</param>
    /// <param name="children">Children; must be non-empty.</param>
    /// <returns>A section <see cref="NavEntry"/>.</returns>
    public static NavEntry SectionWithIndex(byte[] title, byte[] indexPath, NavEntry[] children)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(indexPath);
        ArgumentNullException.ThrowIfNull(children);
        if (title.Length is 0)
        {
            throw new ArgumentException("Title bytes must be non-empty for a section.", nameof(title));
        }

        if (indexPath.Length is 0)
        {
            throw new ArgumentException("Index-path bytes must be non-empty for a section with a landing page.", nameof(indexPath));
        }

        return children.Length is 0 ? throw new ArgumentException(EmptyChildrenError, nameof(children)) : new(title, indexPath, children);
    }

    /// <summary>UTF-8-encodes <paramref name="value"/>; returns an empty array for an empty input.</summary>
    /// <param name="value">Source string.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] EncodeUtf8(string value) =>
        value.Length is 0 ? [] : Encoding.UTF8.GetBytes(value);
}
