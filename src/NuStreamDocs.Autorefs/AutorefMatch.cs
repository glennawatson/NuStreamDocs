// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs;

/// <summary>Captured offsets of one <c>@autoref:&lt;id&gt;</c> match.</summary>
/// <param name="MarkerStart">Index of the leading <c>@</c> of the marker.</param>
/// <param name="IdStart">Index of the first byte after the colon (i.e. the first byte of the ID).</param>
/// <param name="IdEnd">Index of the first byte that terminates the ID (or <c>source.Length</c> when the marker runs to EOF).</param>
internal readonly record struct AutorefMatch(int MarkerStart, int IdStart, int IdEnd);
