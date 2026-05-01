// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown;

/// <summary>
/// Offset/length descriptor of a block discovered by the scanner.
/// </summary>
/// <remarks>
/// Stores positions into the original UTF-8 buffer rather than copying
/// slices, so a parse pass over a large document allocates O(blocks)
/// rather than O(bytes).
/// </remarks>
/// <param name="Kind">Classified block kind.</param>
/// <param name="Start">Start byte offset in the source UTF-8 buffer.</param>
/// <param name="Length">Length in bytes of the full block.</param>
/// <param name="Level">Heading level / list indent / fence length, kind-dependent (0..6 for ATX).</param>
public readonly record struct BlockSpan(
    BlockKind Kind,
    int Start,
    int Length,
    int Level);
