// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc;

/// <summary>
/// A heading the <see cref="HeadingScanner"/> located in rendered HTML.
/// </summary>
/// <remarks>
/// All offsets index into the original UTF-8 byte snapshot. The
/// scanner records the open-tag span (so the rewriter can splice in
/// an <c>id</c> attribute) and the close-tag offset (so the rewriter
/// can insert the permalink anchor immediately before <c>&lt;/hN&gt;</c>).
/// <see cref="ExistingId"/> is non-empty when the heading already had
/// an <c>id</c> attribute the scanner detected — in that case the
/// slugifier preserves the existing value.
/// </remarks>
/// <param name="Level">Heading level (1..6).</param>
/// <param name="OpenTagStart">Byte offset of the <c>&lt;</c> in the open tag.</param>
/// <param name="OpenTagEnd">Byte offset just past the <c>&gt;</c> of the open tag.</param>
/// <param name="CloseTagStart">Byte offset of the <c>&lt;</c> in the close tag.</param>
/// <param name="TextStart">Byte offset of the first byte of inner content.</param>
/// <param name="TextEnd">Byte offset just past the last byte of inner content.</param>
/// <param name="ExistingId">Existing <c>id</c> attribute value, or empty when the heading had none.</param>
/// <param name="Slug">Final slug assigned by <see cref="HeadingSlugifier"/>; empty until that pass runs.</param>
internal readonly record struct Heading(
    int Level,
    int OpenTagStart,
    int OpenTagEnd,
    int CloseTagStart,
    int TextStart,
    int TextEnd,
    string ExistingId,
    string Slug);
