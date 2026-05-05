// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Byte-range record for a single quoted attribute located inside a tag-body span.
/// All offsets are measured against the tag-body span the finder was given.
/// </summary>
/// <param name="Start">Offset of the first byte of the attribute name; <c>-1</c> when not found.</param>
/// <param name="ValueStart">Offset of the first byte of the value (just past the opening quote).</param>
/// <param name="ValueEnd">Offset of the closing quote (i.e. one past the last value byte).</param>
internal readonly record struct NamedAttribute(int Start, int ValueStart, int ValueEnd)
{
    /// <summary>Gets the sentinel value returned when the attribute is absent.</summary>
    public static NamedAttribute None => new(-1, -1, -1);

    /// <summary>Gets a value indicating whether the attribute was located.</summary>
    public bool Found => Start >= 0;
}
