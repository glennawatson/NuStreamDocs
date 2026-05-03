// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace NuStreamDocs.Common;

/// <summary>UTF-8 byte snapshot helpers for <see cref="Utf8JsonReader"/>.</summary>
/// <remarks>
/// Hand-rolled deserializers across the project (privacy, versions, search, …) repeatedly need
/// to copy the current string token's bytes verbatim or pull a JSON array of strings into a
/// <c>byte[][]</c>. These extensions consolidate the multi-segment / single-span branch and the
/// list-pre-size pattern in one place so the call sites stay byte-shaped without boilerplate.
/// </remarks>
[SuppressMessage("Design", "CA1045:Do not pass types by reference", Justification = "Utf8JsonReader is a ref struct; mutating its position requires a ref parameter.")]
public static class Utf8JsonReaderByteExtensions
{
    /// <summary>Returns the current string token's UTF-8 bytes as a fresh array.</summary>
    /// <param name="reader">Reader positioned on a <see cref="JsonTokenType.String"/> token.</param>
    /// <returns>A copy of the token's UTF-8 bytes.</returns>
    public static byte[] CopyStringValueBytes(this ref Utf8JsonReader reader) =>
        reader.HasValueSequence ? CopySequence(reader.ValueSequence) : reader.ValueSpan.ToArray();

    /// <summary>Reads a JSON array of strings as UTF-8 byte snapshots, advancing past the closing bracket.</summary>
    /// <param name="reader">Reader positioned just before the array's opening bracket.</param>
    /// <returns>The parsed bytes; one entry per JSON string. Returns an empty array when the next token isn't an array start.</returns>
    public static byte[][] ReadStringArrayAsBytes(this ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return [];
        }

        var values = new List<byte[]>(4);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                values.Add(reader.CopyStringValueBytes());
            }
        }

        return [.. values];
    }

    /// <summary>Copies a multi-segment <see cref="ReadOnlySequence{T}"/> into a single right-sized array.</summary>
    /// <param name="sequence">Source sequence.</param>
    /// <returns>The copied bytes.</returns>
    private static byte[] CopySequence(ReadOnlySequence<byte> sequence)
    {
        var buffer = new byte[sequence.Length];
        sequence.CopyTo(buffer);
        return buffer;
    }
}
