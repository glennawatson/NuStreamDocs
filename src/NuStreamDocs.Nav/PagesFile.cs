// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Decoded contents of a literate-nav <c>.pages</c> override file (mkdocs-awesome-pages compatible).</summary>
/// <param name="Title">Optional section title bytes; empty when not set.</param>
/// <param name="OrderedEntries">Explicit child ordering. Empty when no <c>nav:</c> key is present.</param>
/// <param name="Hide">When true, the parent should drop this section from the nav tree entirely.</param>
/// <param name="ReverseOrder"><c>order: desc</c> — reverse the section's default child ordering. Ignored when <see cref="OrderedEntries"/> is non-empty.</param>
internal readonly record struct PagesFile(byte[] Title, PagesEntry[] OrderedEntries, bool Hide, bool ReverseOrder)
{
    /// <summary>Gets the empty (no-override) instance.</summary>
    public static PagesFile Empty { get; } = new([], [], false, false);
}
