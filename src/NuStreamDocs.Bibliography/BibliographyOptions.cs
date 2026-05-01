// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Styles;
using NuStreamDocs.Bibliography.Styles.Aglc4;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Configuration for <see cref="BibliographyPlugin"/>.
/// </summary>
/// <param name="Database">Resolved citation database; supplied via the fluent <see cref="BibliographyDatabaseBuilder"/> or one of the loader entry points.</param>
/// <param name="Style">Citation style; defaults to AGLC4.</param>
/// <param name="WarnOnMissing">When true, an unresolved <c>[@key]</c> is logged at <c>Warning</c>.</param>
public sealed record BibliographyOptions(
    BibliographyDatabase Database,
    ICitationStyle Style,
    bool WarnOnMissing)
{
    /// <summary>Gets the default option set — empty database, AGLC4 style, no warnings.</summary>
    public static BibliographyOptions Default { get; } = new(
        Database: BibliographyDatabase.Empty,
        Style: Aglc4Style.Instance,
        WarnOnMissing: false);
}
