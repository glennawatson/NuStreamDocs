// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>One concrete font file resolved for a declared family — a single weight/style/subset, with its woff2 bytes.</summary>
/// <param name="FamilyBytes">UTF-8 CSS family name (e.g. <c>Source Sans 3</c>).</param>
/// <param name="Weight">Numeric font weight (e.g. 400, 700).</param>
/// <param name="Style">Upright or italic.</param>
/// <param name="UnicodeRange">UTF-8 value for the <c>unicode-range</c> descriptor; empty when the file covers everything (e.g. a local font).</param>
/// <param name="Woff2Bytes">The woff2 file contents.</param>
/// <param name="SourceUrl">Where the file came from (a remote URL, or a local file path) — informational; the only string in the module, forced by the HTTP layer.</param>
public readonly record struct FontResource(
    byte[] FamilyBytes,
    int Weight,
    FontStyle Style,
    byte[] UnicodeRange,
    byte[] Woff2Bytes,
    ApiCompatString SourceUrl);
