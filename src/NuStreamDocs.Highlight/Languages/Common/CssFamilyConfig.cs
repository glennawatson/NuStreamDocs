// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Per-language configuration consumed by <see cref="CssFamilyRules.Build"/>.</summary>
/// <remarks>
/// Toggle <see cref="IncludeLineComment"/> for the SCSS / Less <c>//</c> form, and
/// <see cref="VariableSigil"/> selects between SCSS-flavored <c>$var</c> (<c>'$'</c>) and
/// Less-flavored <c>@var</c> (<c>'@'</c>); plain CSS leaves it as <c>0</c> (no sigil rule).
/// </remarks>
internal readonly record struct CssFamilyConfig
{
    /// <summary>Gets a value indicating whether <c>//</c> line comments are recognized.</summary>
    public bool IncludeLineComment { get; init; }

    /// <summary>Gets the sigil byte for variable references (<c>'$'</c> for SCSS, <c>'@'</c> for Less); <c>0</c> disables the rule.</summary>
    public byte VariableSigil { get; init; }

    /// <summary>Gets a value indicating whether the <c>&amp;</c> parent-reference selector is recognized (SCSS / Less).</summary>
    public bool IncludeParentSelector { get; init; }
}
