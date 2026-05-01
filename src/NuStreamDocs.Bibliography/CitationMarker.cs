// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography;

/// <summary>
/// One parsed pandoc-style citation marker — <c>[@key]</c>,
/// <c>[@key, p 23]</c>, <c>[@key1; @key2, p 5]</c>.
/// </summary>
/// <param name="StartIndex">Source offset of the leading <c>[</c>.</param>
/// <param name="EndIndex">Source offset just past the trailing <c>]</c>.</param>
/// <param name="Cites">Parsed citation references in the order they appeared.</param>
public readonly record struct CitationMarker(
    int StartIndex,
    int EndIndex,
    CitationReference[] Cites);
