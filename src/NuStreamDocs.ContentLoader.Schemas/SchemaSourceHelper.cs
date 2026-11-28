// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;
using NuStreamDocs.Config.MkDocs;

namespace NuStreamDocs.ContentLoader.Schemas;

/// <summary>Loads raw schema bytes from local files or in-memory buffers and normalizes YAML/JSON inputs plus Markdown table cells.</summary>
internal static class SchemaSourceHelper
{
    /// <summary>ASCII uppercase-to-lowercase offset.</summary>
    private const int AsciiCaseOffset = 'a' - 'A';

    /// <summary>Extra buffer slack when converting YAML to JSON.</summary>
    private const int YamlBufferPadding = 64;

    /// <summary>Initial buffer capacity for array value summaries.</summary>
    private const int InitialArraySummaryCapacity = 32;

    /// <summary>Resolves and reads a local schema file against <paramref name="inputRoot"/>.</summary>
    /// <param name="schemaFile">Relative or rooted file path.</param>
    /// <param name="inputRoot">Docs input root.</param>
    /// <param name="loaderLabel">Loader name for error messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTF-8 schema bytes.</returns>
    /// <exception cref="ContentLoaderException">Thrown when the file does not exist.</exception>
    internal static Task<byte[]> ReadLocalFileAsync(
        FilePath schemaFile,
        DirectoryPath inputRoot,
        string loaderLabel,
        CancellationToken cancellationToken)
    {
        var resolved = Path.IsPathRooted(schemaFile.Value) || inputRoot.IsEmpty
            ? schemaFile
            : inputRoot.File(schemaFile.Value);

        if (!resolved.Exists())
        {
            throw new ContentLoaderException($"{loaderLabel} file not found: {resolved.Value}");
        }

        return resolved.ReadAllBytesAsync(cancellationToken);
    }

    /// <summary>Converts YAML bytes to UTF-8 JSON when <paramref name="raw"/> does not begin with <c>{</c> or <c>[</c>.</summary>
    /// <param name="raw">UTF-8 JSON or YAML bytes.</param>
    /// <returns>UTF-8 JSON bytes.</returns>
    internal static byte[] NormalizeJsonOrYaml(byte[] raw)
    {
        var idx = AsciiByteHelpers.SkipWhitespace(raw, 0);
        if (idx < raw.Length && raw[idx] is (byte)'{' or (byte)'[')
        {
            return raw;
        }

        var buffer = new ArrayBufferWriter<byte>(raw.Length + YamlBufferPadding);
        using (var jsonWriter = new Utf8JsonWriter(buffer))
        {
            YamlToJson.Convert(raw, jsonWriter);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Composes the target <see cref="FilePath"/> under <paramref name="routePrefix"/> for <paramref name="pageName"/>.</summary>
    /// <param name="routePrefix">Optional subdirectory segment.</param>
    /// <param name="pageName">Markdown filename or slug stem.</param>
    /// <returns>Forward-slashed relative <see cref="FilePath"/> ending in <c>.md</c>.</returns>
    internal static FilePath BuildTargetPath(PathSegment routePrefix, string pageName)
    {
        var normalizedPage = pageName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? pageName
            : $"{Slugify(pageName)}.md";
        var prefix = routePrefix.IsEmpty ? string.Empty : routePrefix.Value.Trim('/');
        return string.IsNullOrEmpty(prefix) ? new(normalizedPage) : new($"{prefix}/{normalizedPage}");
    }

    /// <summary>Converts a title or identifier into a lowercase hyphenated filename stem.</summary>
    /// <param name="value">Raw identifier.</param>
    /// <returns>Non-empty lowercase ASCII slug.</returns>
    internal static string Slugify(string value)
    {
        var chars = new char[value.Length];
        var len = 0;
        var prevDash = true;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsAsciiLetterOrDigit(ch))
            {
                chars[len] = ch is >= 'A' and <= 'Z' ? (char)(ch + AsciiCaseOffset) : ch;
                len++;
                prevDash = false;
            }
            else if (!prevDash)
            {
                chars[len] = '-';
                len++;
                prevDash = true;
            }
        }

        if (len > 0 && chars[len - 1] == '-')
        {
            len--;
        }

        return len == 0 ? "schema" : new string(chars, 0, len);
    }

    /// <summary>Writes a UTF-8 span into a Markdown table cell, escaping <c>|</c> and replacing line breaks with spaces.</summary>
    /// <param name="writer">Destination sink.</param>
    /// <param name="text">Raw UTF-8 text.</param>
    internal static void WriteEscapedTableCell(IBufferWriter<byte> writer, ReadOnlySpan<byte> text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var b = text[i];
            if (b == (byte)'|')
            {
                writer.Write("\\|"u8);
            }
            else if (b is (byte)'\r' or (byte)'\n')
            {
                if (b == (byte)'\n')
                {
                    writer.Write(" "u8);
                }
            }
            else
            {
                var dst = writer.GetSpan(1);
                dst[0] = b;
                writer.Advance(1);
            }
        }
    }

    /// <summary>Writes YAML frontmatter with a double-quoted <c>title</c> scalar.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="title">UTF-8 title bytes.</param>
    internal static void WriteFrontMatterTitle(IBufferWriter<byte> writer, ReadOnlySpan<byte> title)
    {
        writer.Write("---\ntitle: \""u8);
        for (var i = 0; i < title.Length; i++)
        {
            var b = title[i];
            if (b is (byte)'"' or (byte)'\\')
            {
                writer.Write("\\"u8);
            }

            var dst = writer.GetSpan(1);
            dst[0] = b;
            writer.Advance(1);
        }

        writer.Write("\"\n---\n\n# "u8);
        writer.Write(title);
        writer.Write("\n\n"u8);
    }

    /// <summary>Advances <paramref name="reader"/> to the root token and throws when it is not a JSON object.</summary>
    /// <param name="reader">Reader at start of input.</param>
    /// <param name="errorMessage">Exception message when root is not a JSON object.</param>
    /// <exception cref="ContentLoaderException">Thrown when the root token is not <see cref="JsonTokenType.StartObject"/>.</exception>
    internal static void EnsureRootObject(ref Utf8JsonReader reader, string errorMessage)
    {
        _ = reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new ContentLoaderException(errorMessage);
        }
    }

    /// <summary>Advances <paramref name="reader"/> inside a JSON object and returns <c>true</c> while on a property.</summary>
    /// <param name="reader">Active JSON reader.</param>
    /// <returns><c>true</c> if positioned at a property name; <c>false</c> at <see cref="JsonTokenType.EndObject"/>.</returns>
    internal static bool ReadNextProperty(ref Utf8JsonReader reader)
    {
        _ = reader.Read();
        return reader.TokenType != JsonTokenType.EndObject;
    }

    /// <summary>Advances <paramref name="reader"/> inside a JSON array and returns <c>true</c> while not at <see cref="JsonTokenType.EndArray"/>.</summary>
    /// <param name="reader">Active JSON reader.</param>
    /// <returns><c>true</c> if positioned at an element; <c>false</c> at <see cref="JsonTokenType.EndArray"/>.</returns>
    internal static bool ReadNextElement(ref Utf8JsonReader reader)
    {
        _ = reader.Read();
        return reader.TokenType != JsonTokenType.EndArray;
    }

    /// <summary>Formats a raw JSON scalar or composite token value into concise UTF-8 text.</summary>
    /// <param name="reader">Reader positioned at the value token.</param>
    /// <returns>UTF-8 representation.</returns>
    internal static byte[] ReadJsonValueSummary(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
            {
                return Encoding.UTF8.GetBytes(reader.GetString()!);
            }

            case JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False:
            {
                return reader.ValueSpan.ToArray();
            }

            case JsonTokenType.StartArray:
            {
                return ReadArraySummary(ref reader);
            }

            default:
            {
                reader.Skip();
                return [];
            }
        }
    }

    /// <summary>Joins scalar items of a JSON array with <c>, </c>.</summary>
    /// <param name="reader">Reader positioned at <see cref="JsonTokenType.StartArray"/>.</param>
    /// <returns>Joined UTF-8 summary.</returns>
    private static byte[] ReadArraySummary(ref Utf8JsonReader reader)
    {
        var writer = new ArrayBufferWriter<byte>(InitialArraySummaryCapacity);
        var first = true;
        while (ReadNextElement(ref reader))
        {
            var item = ReadJsonValueSummary(ref reader);
            if (item.Length == 0)
            {
                continue;
            }

            if (!first)
            {
                writer.Write(", "u8);
            }

            first = false;
            writer.Write(item);
        }

        return writer.WrittenSpan.ToArray();
    }
}
