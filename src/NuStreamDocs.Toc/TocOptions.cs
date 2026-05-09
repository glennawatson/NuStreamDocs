// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc;

/// <summary>Configuration for <see cref="TocPlugin"/>; mirrors the mkdocs <c>toc</c> markdown extension.</summary>
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
