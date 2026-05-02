// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography.Model;

/// <summary>
/// Represents a single author / editor / contributor name. Field names
/// align with CSL-JSON's name model so a future CSL backend slots in
/// without schema migration.
/// </summary>
/// <param name="Family">UTF-8 family / surname bytes (CSL <c>family</c>).</param>
/// <param name="Given">UTF-8 given / forename bytes (CSL <c>given</c>); may be empty for institutional authors.</param>
/// <param name="Suffix">UTF-8 generational or honorary suffix bytes ("Jr.", "III", "QC"); may be empty.</param>
/// <param name="Literal">UTF-8 institutional-name bytes (CSL <c>literal</c>) — when non-empty, treats the name as a single string with no parts.</param>
/// <remarks>
/// All fields are byte-shaped so the AGLC4 emitter copies them straight into its
/// <see cref="System.Buffers.IBufferWriter{T}"/> sinks without per-emit transcoding.
/// String constructors are provided for callers building names from C# literals
/// or YAML/CSL string sources; they encode once at the boundary.
/// </remarks>
public sealed record PersonName(
    byte[] Family,
    byte[] Given,
    byte[] Suffix,
    byte[] Literal)
{
    /// <summary>Gets a value indicating whether this name is institutional (literal-only).</summary>
    public bool IsInstitutional => Literal.Length > 0;

    /// <summary>Convenience for hand-rolling a Given/Family name from C# strings.</summary>
    /// <param name="given">Given name.</param>
    /// <param name="family">Family name.</param>
    /// <returns>The name record.</returns>
    public static PersonName Of(string given, string family) =>
        new(
            Family: Utf8Encoder.Encode(family),
            Given: Utf8Encoder.Encode(given),
            Suffix: [],
            Literal: []);

    /// <summary>Convenience for institutional authors (the whole string lives in <see cref="Literal"/>).</summary>
    /// <param name="literal">The institution name.</param>
    /// <returns>The name record.</returns>
    public static PersonName Institutional(string literal) =>
        new(
            Family: [],
            Given: [],
            Suffix: [],
            Literal: Utf8Encoder.Encode(literal));
}
