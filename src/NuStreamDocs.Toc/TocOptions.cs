// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc;

/// <summary>
/// Configuration for <see cref="TocPlugin"/>.
/// </summary>
/// <remarks>
/// Mirrors the most-used knobs of the mkdocs <c>toc</c> markdown
/// extension. Defaults are tuned for mkdocs-material's secondary
/// nav: only <c>h2</c>+ are listed, the permalink glyph is the
/// pilcrow, and the marker substitution is enabled so themes can
/// embed <c>&lt;!--@@toc@@--&gt;</c> in their templates.
/// </remarks>
/// <param name="MinLevel">Minimum heading level (inclusive) to include in the rendered TOC fragment. Permalink anchors are still emitted on every heading the scanner finds.</param>
/// <param name="MaxLevel">Maximum heading level (inclusive) to include in the rendered TOC fragment.</param>
/// <param name="PermalinkSymbol">Glyph used inside the permalink anchor (e.g. <c>¶</c>, <c>#</c>). Rendered as the anchor's inner text.</param>
/// <param name="MarkerSubstitute">When true, <see cref="TocPlugin"/> looks for <c>&lt;!--@@toc@@--&gt;</c> in the rendered HTML and replaces it with the rendered TOC fragment.</param>
public readonly record struct TocOptions(
    int MinLevel,
    int MaxLevel,
    string PermalinkSymbol,
    bool MarkerSubstitute)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static TocOptions Default { get; } = new(
        MinLevel: 2,
        MaxLevel: 6,
        PermalinkSymbol: "¶",
        MarkerSubstitute: true);
}
