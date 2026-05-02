// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography.Model;

/// <summary>
/// Pinpoint locator inside a citation — the <c>p 23</c>, <c>[12]</c>,
/// <c>ch 4</c> portion that pandoc captures in <c>[@key, p 23]</c>.
/// </summary>
/// <remarks>
/// The label is classified at parse time into <see cref="LocatorKind"/> so
/// the formatter dispatches on an enum (no per-locator <see cref="string"/>
/// allocation, no <c>ToUpperInvariant</c> on the hot path). The label
/// surface is intentionally finite — the AGLC4 / pandoc / CSL universe of
/// pinpoint kinds fits in a byte. Unknown labels round-trip through
/// <see cref="LocatorKind.Other"/>. The value is held as a <c>(start, length)</c>
/// pair into the rewriter's source span so parsing produces no per-locator
/// heap allocation; the style is given the source span at emit time and
/// slices the value bytes directly.
/// </remarks>
/// <param name="Kind">Classified locator kind; <see cref="LocatorKind.None"/> when bare.</param>
/// <param name="Start">Inclusive byte offset of the value within the rewriter's source span; ignored when <paramref name="Length"/> is zero.</param>
/// <param name="Length">Byte length of the value within the source span; zero for <see cref="None"/>.</param>
public readonly record struct CitationLocator(LocatorKind Kind, int Start, int Length)
{
    /// <summary>Gets the empty / no-locator sentinel.</summary>
    public static CitationLocator None => default;

    /// <summary>Gets a value indicating whether a locator was specified.</summary>
    public bool HasValue => Length > 0;
}
