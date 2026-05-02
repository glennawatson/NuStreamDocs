// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>
/// Decoded contents of a literate-nav <c>.pages</c> override file
/// (mkdocs-awesome-pages compatible, minimal subset). All fields default
/// to "no override".
/// </summary>
/// <param name="Title">Optional section title bytes; empty when not set.</param>
/// <param name="OrderedEntries">
/// Explicit child ordering bytes; entries may reference page filenames
/// (e.g. <c>intro.md</c>) or subsection directory names. Empty when no
/// <c>nav:</c> key is present.
/// </param>
/// <param name="Hide">When true, the parent should drop this section from the nav tree entirely.</param>
/// <remarks>Byte-shaped so the YAML reader emits the parsed bytes directly without UTF-8 → string transcoding; consumers that need a <see cref="string"/> decode at the boundary.</remarks>
internal readonly record struct PagesFile(byte[] Title, byte[][] OrderedEntries, bool Hide)
{
    /// <summary>Gets the empty (no-override) instance.</summary>
    public static PagesFile Empty { get; } = new([], [], Hide: false);
}
