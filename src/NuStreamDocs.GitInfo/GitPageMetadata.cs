// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.GitInfo;

/// <summary>Resolved revision date and distinct contributor names for a single documentation page.</summary>
/// <param name="LastUpdatedIsoDate">UTF-8 ISO-8601 date (<c>YYYY-MM-DD</c>).</param>
/// <param name="Contributors">Distinct UTF-8 contributor display names ordered from most recent to oldest.</param>
[System.Diagnostics.DebuggerDisplay("GitPageMetadata: {Contributors.Length} contributors")]
public readonly record struct GitPageMetadata(byte[] LastUpdatedIsoDate, byte[][] Contributors);
