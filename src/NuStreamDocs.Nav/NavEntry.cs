// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Dialect-neutral navigation entry. Leaves carry a non-empty <see cref="Path"/>; sections carry non-empty <see cref="Children"/>; sections with a landing page carry both.</summary>
/// <param name="Title">UTF-8 display title; empty to derive from the source file (front-matter, first heading, or filename).</param>
/// <param name="Path">UTF-8 source-relative markdown path (forward slashes), absolute URL (http/https), or empty for a pure section.</param>
/// <param name="Children">Nested entries; empty for a leaf.</param>
public readonly record struct NavEntry(byte[] Title, byte[] Path, NavEntry[] Children)
{
    /// <summary>Gets a value indicating whether this entry represents a section (has children).</summary>
    public bool IsSection => Children.Length > 0;
}
