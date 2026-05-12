// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>One declared font family in a <see cref="FontsOptions"/> config.</summary>
/// <param name="Id">UTF-8 identifier used in the generated <c>--nstd-font-&lt;id&gt;</c> variable and the fallback face name.</param>
/// <param name="FamilyBytes">UTF-8 CSS family name.</param>
/// <param name="Provider">Where the files are resolved from at build time.</param>
/// <param name="Weights">Numeric weights to fetch.</param>
/// <param name="Styles">Styles to fetch.</param>
/// <param name="Subsets">UTF-8 subset names; the single name <c>"all"u8</c> requests every subset the provider offers.</param>
/// <param name="Display">CSS <c>font-display</c> descriptor.</param>
/// <param name="Preload">Whether the weight-400 / normal face gets a preload link.</param>
/// <param name="Fallback">Generic fallback family; also picks the system reference font for the CLS overrides.</param>
/// <param name="LocalSrc">Glob patterns (relative to the input root) for the files; only used when <see cref="Provider"/> is <see cref="FontProviderKind.Local"/>.</param>
/// <param name="ThemeVariables">UTF-8 names of CSS custom properties this face should drive (each set to <c>var(--nstd-font-&lt;id&gt;)</c>).</param>
[SuppressMessage("Major Code Smell", "S107", Justification = "A font-face declaration has this many orthogonal knobs.")]
public readonly record struct FontFace(
    byte[] Id,
    byte[] FamilyBytes,
    FontProviderKind Provider,
    int[] Weights,
    FontStyle[] Styles,
    byte[][] Subsets,
    FontDisplay Display,
    bool Preload,
    GenericFontFamily Fallback,
    GlobPattern[] LocalSrc,
    byte[][] ThemeVariables);
