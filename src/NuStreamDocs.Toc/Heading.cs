// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc;

/// <summary>
/// A heading the <see cref="HeadingScanner"/> located in rendered HTML.
/// </summary>
/// <remarks>
/// All offsets index into the original UTF-8 byte snapshot. Existing
/// <c>id</c> attribute values are stored as offset+length so the
/// scanner never UTF-8 decodes them; the slug is computed once into a
/// byte array (always ASCII per the slug rule) so the rewrite and TOC
/// fragment passes can splice it back into the output stream without
/// any further allocation or transcoding.
/// </remarks>
/// <param name="Level">Heading level (1..6).</param>
/// <param name="OpenTagStart">Byte offset of the <c>&lt;</c> in the open tag.</param>
/// <param name="OpenTagEnd">Byte offset just past the <c>&gt;</c> of the open tag.</param>
/// <param name="CloseTagStart">Byte offset of the <c>&lt;</c> in the close tag.</param>
/// <param name="TextStart">Byte offset of the first byte of inner content.</param>
/// <param name="TextEnd">Byte offset just past the last byte of inner content.</param>
/// <param name="ExistingIdStart">Byte offset of an existing <c>id</c> attribute value, or <c>-1</c> when none.</param>
/// <param name="ExistingIdLength">Length of the existing <c>id</c> attribute value, or <c>0</c> when none.</param>
/// <param name="Slug">Final slug bytes assigned by <see cref="HeadingSlugifier"/>; ASCII per the slug rule. Empty until that pass runs.</param>
internal readonly record struct Heading(
    int Level,
    int OpenTagStart,
    int OpenTagEnd,
    int CloseTagStart,
    int TextStart,
    int TextEnd,
    int ExistingIdStart,
    int ExistingIdLength,
    byte[] Slug)
{
    /// <summary>Gets a value indicating whether the scanner found an existing <c>id</c> attribute on this heading's open tag.</summary>
    public bool HasExistingId => ExistingIdLength > 0;

    /// <summary>Returns the bytes of the existing <c>id</c> attribute value, sliced from <paramref name="html"/>.</summary>
    /// <param name="html">Original HTML snapshot.</param>
    /// <returns>The id bytes, or an empty span when no existing id was found.</returns>
    public ReadOnlySpan<byte> ExistingIdBytes(ReadOnlySpan<byte> html) =>
        ExistingIdLength is 0 ? default : html.Slice(ExistingIdStart, ExistingIdLength);
}
