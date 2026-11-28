// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;
using NuStreamDocs.Config.MkDocs;

namespace NuStreamDocs.Glossary;

/// <summary>Loads and normalizes glossary definitions from YAML/JSON files and <see cref="GlossaryOptions.Terms"/>.</summary>
internal static class GlossaryLoader
{
    /// <summary>Extra capacity when converting YAML to UTF-8 JSON.</summary>
    private const int YamlBufferSlack = 64;

    /// <summary>Converts <paramref name="term"/> to a lowercase hyphenated ASCII anchor slug.</summary>
    /// <param name="term">UTF-8 term bytes.</param>
    /// <returns>Non-empty UTF-8 slug bytes.</returns>
    internal static byte[] Slugify(ReadOnlySpan<byte> term)
    {
        var buffer = new byte[term.Length];
        var len = 0;
        var prevHyphen = true;
        for (var i = 0; i < term.Length; i++)
        {
            var b = term[i];
            if (AsciiByteHelpers.IsAsciiLetter(b) || AsciiByteHelpers.IsAsciiDigit(b))
            {
                buffer[len] = AsciiByteHelpers.ToAsciiLowerByte(b);
                len++;
                prevHyphen = false;
            }
            else if (!prevHyphen)
            {
                buffer[len] = (byte)'-';
                len++;
                prevHyphen = true;
            }
        }

        if (len > 0 && buffer[len - 1] == (byte)'-')
        {
            len--;
        }

        return len == 0 ? [.. "term"u8] : buffer[..len];
    }

    /// <summary>Loads glossary entries from <paramref name="options"/> and optional <paramref name="inputRoot"/> file.</summary>
    /// <param name="options">Glossary configuration.</param>
    /// <param name="inputRoot">Docs input root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Alphabetically sorted entries for the index page and longest-first entries for HTML matching.</returns>
    internal static async ValueTask<(GlossaryEntry[] SortedForPage, GlossaryEntry[] LongestFirst)> LoadAsync(
        GlossaryOptions options,
        DirectoryPath inputRoot,
        CancellationToken cancellationToken)
    {
        Dictionary<string, GlossaryEntry> merged = [with(StringComparer.OrdinalIgnoreCase)];
        for (var i = 0; i < options.Terms.Length; i++)
        {
            var item = options.Terms[i];
            merged[Encoding.UTF8.GetString(item.Term)] = item;
        }

        var file = ResolveFile(options.GlossaryFile, inputRoot);
        if (!file.IsEmpty && file.Exists())
        {
            var raw = await file.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
            ParseGlossaryBytes(raw, merged);
        }

        GlossaryEntry[] alphabetical = [.. merged.Values];
        Array.Sort(alphabetical, static (a, b) => CompareAsciiIgnoreCase(a.Term, b.Term));

        GlossaryEntry[] longestFirst = [.. alphabetical];
        Array.Sort(longestFirst, static (a, b) =>
        {
            var byLength = b.Term.Length.CompareTo(a.Term.Length);
            return byLength != 0 ? byLength : CompareAsciiIgnoreCase(a.Term, b.Term);
        });

        return (alphabetical, longestFirst);
    }

    /// <summary>Resolves <paramref name="glossaryFile"/> relative to <paramref name="inputRoot"/> when unrooted.</summary>
    /// <param name="glossaryFile">Configured glossary file.</param>
    /// <param name="inputRoot">Docs input root.</param>
    /// <returns>Resolved <see cref="FilePath"/>, or default when empty.</returns>
    private static FilePath ResolveFile(FilePath glossaryFile, DirectoryPath inputRoot)
    {
        if (glossaryFile.IsEmpty)
        {
            return default;
        }

        return Path.IsPathRooted(glossaryFile.Value) || inputRoot.IsEmpty
            ? glossaryFile
            : inputRoot.File(glossaryFile.Value);
    }

    /// <summary>Parses YAML or JSON glossary definitions into <paramref name="target"/>.</summary>
    /// <param name="raw">Raw UTF-8 file bytes.</param>
    /// <param name="target">Destination map keyed by case-insensitive term string.</param>
    private static void ParseGlossaryBytes(byte[] raw, Dictionary<string, GlossaryEntry> target)
    {
        var firstNonWs = AsciiByteHelpers.SkipWhitespace(raw, 0);
        if (firstNonWs >= raw.Length)
        {
            return;
        }

        byte[] jsonBytes;
        if (raw[firstNonWs] is (byte)'{' or (byte)'[')
        {
            jsonBytes = raw;
        }
        else
        {
            ArrayBufferWriter<byte> converted = new(raw.Length + YamlBufferSlack);
            using (Utf8JsonWriter writer = new(converted))
            {
                YamlToJson.Convert(raw, writer);
            }

            jsonBytes = converted.WrittenSpan.ToArray();
        }

        Utf8JsonReader reader = new(jsonBytes);
        _ = reader.Read();
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            ReadObjectGlossary(ref reader, target);
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            ReadArrayGlossary(ref reader, target);
        }
    }

    /// <summary>Reads <c>{ "Term": "Definition" }</c> map entries.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="target">Destination map.</param>
    private static void ReadObjectGlossary(ref Utf8JsonReader reader, Dictionary<string, GlossaryEntry> target)
    {
        while (ReadNextObjectProperty(ref reader))
        {
            var termText = reader.GetString()!;
            _ = reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                AddIfValid(target, termText, reader.GetString()!);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    /// <summary>Reads <c>[ { "term": "...", "definition": "..." } ]</c> array entries.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartArray"/>.</param>
    /// <param name="target">Destination map.</param>
    private static void ReadArrayGlossary(ref Utf8JsonReader reader, Dictionary<string, GlossaryEntry> target)
    {
        while (ReadNextArrayItem(ref reader))
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            ReadArrayEntryObject(ref reader, target);
        }
    }

    /// <summary>Reads one <c>{ "term": "...", "definition": "..." }</c> object.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="target">Destination map.</param>
    private static void ReadArrayEntryObject(ref Utf8JsonReader reader, Dictionary<string, GlossaryEntry> target)
    {
        var term = string.Empty;
        var definition = string.Empty;
        while (ReadNextObjectProperty(ref reader))
        {
            var key = reader.GetString()!;
            _ = reader.Read();
            if (key == "term" && reader.TokenType == JsonTokenType.String)
            {
                term = reader.GetString()!;
            }
            else if (key == "definition" && reader.TokenType == JsonTokenType.String)
            {
                definition = reader.GetString()!;
            }
            else
            {
                reader.Skip();
            }
        }

        AddIfValid(target, term, definition);
    }

    /// <summary>Stores a glossary entry when both <paramref name="term"/> and <paramref name="definition"/> are non-whitespace.</summary>
    /// <param name="target">Destination dictionary.</param>
    /// <param name="term">Term string.</param>
    /// <param name="definition">Definition string.</param>
    private static void AddIfValid(Dictionary<string, GlossaryEntry> target, string term, string definition)
    {
        if (string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(definition))
        {
            return;
        }

        var termBytes = Encoding.UTF8.GetBytes(term);
        target[term] = new(termBytes, Encoding.UTF8.GetBytes(definition), Slugify(termBytes));
    }

    /// <summary>Advances <paramref name="reader"/> inside a JSON object until <see cref="JsonTokenType.EndObject"/>.</summary>
    /// <param name="reader">JSON reader.</param>
    /// <returns><c>true</c> while positioned on a property.</returns>
    private static bool ReadNextObjectProperty(ref Utf8JsonReader reader)
    {
        _ = reader.Read();
        return reader.TokenType != JsonTokenType.EndObject;
    }

    /// <summary>Advances <paramref name="reader"/> inside a JSON array until <see cref="JsonTokenType.EndArray"/>.</summary>
    /// <param name="reader">JSON reader.</param>
    /// <returns><c>true</c> while positioned on an array element.</returns>
    private static bool ReadNextArrayItem(ref Utf8JsonReader reader)
    {
        _ = reader.Read();
        return reader.TokenType != JsonTokenType.EndArray;
    }

    /// <summary>Compares two UTF-8 byte arrays ASCII case-insensitively.</summary>
    /// <param name="left">Left span.</param>
    /// <param name="right">Right span.</param>
    /// <returns>Signed ordering.</returns>
    private static int CompareAsciiIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var min = Math.Min(left.Length, right.Length);
        for (var i = 0; i < min; i++)
        {
            var cmp = AsciiByteHelpers.ToAsciiLowerByte(left[i]).CompareTo(AsciiByteHelpers.ToAsciiLowerByte(right[i]));
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
