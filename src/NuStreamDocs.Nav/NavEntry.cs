// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>
/// Single navigation entry — the dialect-neutral DTO every config-file reader (mkdocs.yml,
/// docfx <c>toc.yml</c>, awesome-nav, zensical TOML) emits and
/// <see cref="NavOptions.CuratedEntries"/> consumes. A leaf page carries a non-empty
/// <see cref="Path"/>; a section carries a non-empty <see cref="Children"/> array; a section that
/// also names a landing page (e.g. <c>guide/index.md</c>) carries both. External links carry an
/// absolute URL in <see cref="Path"/>.
/// </summary>
/// <param name="Title">
/// UTF-8 display title; empty when the entry was supplied as a bare path and the renderer
/// should derive a title from the source file (front-matter, first heading, or filename).
/// </param>
/// <param name="Path">UTF-8 source-relative markdown path (forward slashes), absolute URL (http/https), or empty for a pure section without a landing page.</param>
/// <param name="Children">Nested entries; empty for a leaf.</param>
public readonly record struct NavEntry(byte[] Title, byte[] Path, NavEntry[] Children)
{
    /// <summary>Gets a value indicating whether this entry represents a section (has children).</summary>
    public bool IsSection => Children.Length > 0;
}
