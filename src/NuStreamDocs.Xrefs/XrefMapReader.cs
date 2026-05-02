// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace NuStreamDocs.Xrefs;

/// <summary>
/// Parses a DocFX-compatible <c>xrefmap.json</c> document into the
/// <c>(uid, href)</c> pairs <see cref="XrefsPlugin"/> can register
/// into the shared autorefs registry.
/// </summary>
/// <remarks>
/// Tolerant: unknown top-level fields are ignored; entries missing
/// either <c>uid</c> or <c>href</c> are skipped. The reader uses
/// <see cref="Utf8JsonReader"/> directly so the file is parsed
/// straight from UTF-8 bytes; values are unescaped into fresh
/// <see cref="byte"/> arrays via <see cref="Utf8JsonReader.CopyString(System.Span{byte})"/>
/// so the registry hot path receives byte arrays without going through
/// <see cref="string"/>.
/// </remarks>
internal static class XrefMapReader
{
    /// <summary>Decodes <paramref name="bytes"/> into a list of <c>(uid, href)</c> UTF-8 byte pairs and the document's <c>baseUrl</c> bytes (empty when absent).</summary>
    /// <param name="bytes">UTF-8 file contents.</param>
    /// <returns>Parsed result.</returns>
    public static XrefMapPayload Read(ReadOnlySpan<byte> bytes)
    {
        var entries = new List<(byte[] Uid, byte[] Href)>(64);
        byte[] baseUrl = [];

        var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);
        if (!reader.Read() || reader.TokenType is not JsonTokenType.StartObject)
        {
            return new(baseUrl, []);
        }

        while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("baseUrl"u8))
            {
                baseUrl = ReadStringValue(ref reader);
                continue;
            }

            if (reader.ValueTextEquals("references"u8))
            {
                reader.Read();
                ReadReferences(ref reader, entries);
                continue;
            }

            // Unknown top-level key: skip its value.
            reader.Read();
            reader.Skip();
        }

        return new(baseUrl, [.. entries]);
    }

    /// <summary>Reads the <c>references</c> array into <paramref name="entries"/>.</summary>
    /// <param name="reader">Reader positioned on <see cref="JsonTokenType.StartArray"/>.</param>
    /// <param name="entries">Destination list.</param>
    private static void ReadReferences(ref Utf8JsonReader reader, List<(byte[] Uid, byte[] Href)> entries)
    {
        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            return;
        }

        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            if (reader.TokenType is not JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            if (TryReadReference(ref reader, out var entry))
            {
                entries.Add(entry);
            }
        }
    }

    /// <summary>Reads a single <c>{ uid, href, ... }</c> object; returns <see langword="false"/> when either required field is missing.</summary>
    /// <param name="reader">Reader positioned on <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="entry">Set to the parsed pair on success.</param>
    /// <returns>True when the object had both <c>uid</c> and <c>href</c>.</returns>
    private static bool TryReadReference(ref Utf8JsonReader reader, out (byte[] Uid, byte[] Href) entry)
    {
        byte[] uid = [];
        byte[] href = [];

        while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("uid"u8))
            {
                uid = ReadStringValue(ref reader);
                continue;
            }

            if (reader.ValueTextEquals("href"u8))
            {
                href = ReadStringValue(ref reader);
                continue;
            }

            // Unknown key (name, fullName, commentId, etc.) — skip.
            reader.Read();
            reader.Skip();
        }

        if (uid.Length is 0 || href.Length is 0)
        {
            entry = default;
            return false;
        }

        entry = (uid, href);
        return true;
    }

    /// <summary>Advances past a property-name token and returns the immediately-following string value as fresh UTF-8 bytes, or empty when the next token isn't a string.</summary>
    /// <param name="reader">Reader positioned on the property-name token.</param>
    /// <returns>UTF-8 bytes of the string value, or an empty array.</returns>
    /// <remarks>
    /// Uses <see cref="Utf8JsonReader.CopyString(System.Span{byte})"/> so escape sequences
    /// (<c>\n</c>, <c> </c>, …) are decoded without round-tripping through a UTF-16
    /// <see cref="string"/>.
    /// </remarks>
    private static byte[] ReadStringValue(ref Utf8JsonReader reader)
    {
        reader.Read();
        if (reader.TokenType is not JsonTokenType.String)
        {
            return [];
        }

        // ValueSpan length is an upper bound — escapes only shrink the unescaped output.
        var maxLength = reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length;
        if (maxLength is 0)
        {
            return [];
        }

        var dst = new byte[maxLength];
        var written = reader.CopyString(dst);
        return written == dst.Length ? dst : dst[..written];
    }
}
