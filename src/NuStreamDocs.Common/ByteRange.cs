// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Half-open byte range <c>[Start, Start + Length)</c> over a UTF-8
/// snapshot — the canonical "match offsets" shape returned by the
/// byte-only scanners (LinkExtractor, AutorefScanner-style helpers,
/// HeadingScanner).
/// </summary>
/// <param name="Start">Inclusive start offset into the snapshot.</param>
/// <param name="Length">Byte length of the matched span.</param>
public readonly record struct ByteRange(int Start, int Length)
{
    /// <summary>Gets a value indicating whether the range covers any bytes.</summary>
    public bool IsEmpty => Length is 0;

    /// <summary>Slices the byte range out of <paramref name="source"/>.</summary>
    /// <param name="source">UTF-8 snapshot the offsets index into.</param>
    /// <returns>The matched span; an empty span when <see cref="Length"/> is 0.</returns>
    public ReadOnlySpan<byte> AsSpan(ReadOnlySpan<byte> source) =>
        Length is 0 ? default : source.Slice(Start, Length);
}
