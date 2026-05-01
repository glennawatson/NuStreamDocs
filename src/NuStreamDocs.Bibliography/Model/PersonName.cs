// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography.Model;

/// <summary>
/// Represents a single author / editor / contributor name. Field names
/// align with CSL-JSON's name model so a future CSL backend slots in
/// without schema migration.
/// </summary>
/// <param name="Family">Family / surname (CSL <c>family</c>).</param>
/// <param name="Given">Given / forename(s) (CSL <c>given</c>); may be empty for institutional authors.</param>
/// <param name="Suffix">Optional generational or honorary suffix ("Jr.", "III", "QC"). May be empty.</param>
/// <param name="Literal">When non-empty, treats the name as a single string with no parts (CSL <c>literal</c>) — institutional authors, "Anonymous", etc.</param>
public sealed record PersonName(
    string Family,
    string Given,
    string Suffix,
    string Literal)
{
    /// <summary>Gets a value indicating whether returns true when this name is institutional (literal-only).</summary>
    public bool IsInstitutional => Literal.Length > 0;

    /// <summary>Convenience for hand-rolling a Given/Family name from C#.</summary>
    /// <param name="given">Given name.</param>
    /// <param name="family">Family name.</param>
    /// <returns>The name record.</returns>
    public static PersonName Of(string given, string family) =>
        new(Family: family, Given: given, Suffix: string.Empty, Literal: string.Empty);

    /// <summary>Convenience for institutional authors (the whole string lives in <see cref="Literal"/>).</summary>
    /// <param name="literal">The institution name.</param>
    /// <returns>The name record.</returns>
    public static PersonName Institutional(string literal) =>
        new(Family: string.Empty, Given: string.Empty, Suffix: string.Empty, Literal: literal);
}
