// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown;

/// <summary>An opening and a closing delimiter that wrap one emphasis or strong span.</summary>
/// <param name="OpenStart">Index of the first byte of the opening delimiter.</param>
/// <param name="CloseStart">Index of the first byte of the closing delimiter.</param>
/// <param name="Length">Delimiter length: 1 for emphasis, 2 for strong.</param>
/// <param name="Next">One-based index of the next inner pair opened by the same run, or zero.</param>
internal readonly record struct EmphasisPair(int OpenStart, int CloseStart, int Length, int Next);
