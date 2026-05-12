// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts;

/// <summary>The vertical metrics of a font, in font design units, needed to compute a CLS-free system-font fallback.</summary>
/// <param name="UnitsPerEm">Design units per em (the <c>head</c> table's <c>unitsPerEm</c>).</param>
/// <param name="Ascender">Typographic ascender (the <c>hhea</c> table's <c>ascender</c>).</param>
/// <param name="Descender">Typographic descender (the <c>hhea</c> table's <c>descender</c>; normally negative).</param>
/// <param name="LineGap">Typographic line gap (the <c>hhea</c> table's <c>lineGap</c>).</param>
/// <param name="XHeight">Height of a lowercase <c>x</c> (the <c>OS/2</c> table's <c>sxHeight</c>; zero when the font omits it).</param>
/// <param name="CapHeight">Height of a capital letter (the <c>OS/2</c> table's <c>sCapHeight</c>; zero when the font omits it).</param>
public readonly record struct FontMetrics(
    int UnitsPerEm,
    int Ascender,
    int Descender,
    int LineGap,
    int XHeight,
    int CapHeight);
