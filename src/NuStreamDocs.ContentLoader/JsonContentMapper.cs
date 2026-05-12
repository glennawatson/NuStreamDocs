// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.ContentLoader.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader;

/// <summary>Turns a JSON document (or JSON converted from YAML) into <see cref="SyntheticPage"/> entries according to a <see cref="ContentMapping"/>.</summary>
internal static class JsonContentMapper
{
    /// <summary>Headroom added to the route-writer capacity hint for substituted placeholder values.</summary>
    private const int RouteHeadroom = 16;

    /// <summary>Initial capacity for the reused Markdown writer; pages are typically a few hundred bytes.</summary>
    private const int InitialMarkdownCapacity = 256;

    /// <summary>Maps <paramref name="json"/> through <paramref name="mapping"/>.</summary>
    /// <param name="json">UTF-8 JSON bytes.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="loaderName">Loader name for diagnostics.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The produced pages, in document order.</returns>
    /// <exception cref="ContentLoaderException">When the JSON is malformed.</exception>
    public static SyntheticPage[] Map(byte[] json, ContentMapping mapping, ReadOnlySpan<byte> loaderName, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(mapping);
        mapping.Validate();

        var name = Encoding.UTF8.GetString(loaderName);
        var routeTemplate = Encoding.UTF8.GetString(mapping.RouteTemplate);
        using var document = ParseOrThrow(json);
        if (!TryNavigateToArray(document.RootElement, mapping.CollectionPointer, out var array))
        {
            var pointer = Encoding.UTF8.GetString(mapping.CollectionPointer);
            ContentLoaderLoggingHelper.LogCollectionPointerMissed(logger, name, pointer);
            return [];
        }

        List<SyntheticPage> pages = new(array.GetArrayLength());

        // Two writers reused across every entry — the per-entry byte arrays are extracted with
        // ToArray(), so reusing the growing internal buffers avoids one allocation per page.
        ArrayBufferWriter<byte> routeWriter = new(mapping.RouteTemplate.Length + RouteHeadroom);
        ArrayBufferWriter<byte> markdownWriter = new(InitialMarkdownCapacity);

        // foreach over JsonElement.ArrayEnumerator — a struct enumerator with no indexed alternative.
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (TryBuildPage(element, mapping, routeWriter, markdownWriter, out var page))
            {
                pages.Add(page);
            }
            else
            {
                ContentLoaderLoggingHelper.LogSkippedEntry(logger, name, routeTemplate);
            }
        }

        return [.. pages];
    }

    /// <summary>Parses <paramref name="json"/>, wrapping syntax errors in a <see cref="ContentLoaderException"/>.</summary>
    /// <param name="json">UTF-8 JSON bytes.</param>
    /// <returns>The parsed document.</returns>
    private static JsonDocument ParseOrThrow(byte[] json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ContentLoaderException("The source is not valid JSON.", ex);
        }
    }

    /// <summary>Walks <paramref name="pointer"/> from <paramref name="root"/> to the target array.</summary>
    /// <param name="root">Document root.</param>
    /// <param name="pointer">Dotted object-property path; empty means the root is the array.</param>
    /// <param name="array">On success, the located array element.</param>
    /// <returns><see langword="true"/> when the path resolves to a JSON array.</returns>
    private static bool TryNavigateToArray(JsonElement root, byte[] pointer, out JsonElement array)
    {
        var current = root;
        var i = 0;
        while (i < pointer.Length)
        {
            var dot = Array.IndexOf(pointer, (byte)'.', i);
            var end = dot < 0 ? pointer.Length : dot;
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(pointer.AsSpan(i, end - i), out current))
            {
                array = default;
                return false;
            }

            i = end + 1;
        }

        array = current;
        return current.ValueKind == JsonValueKind.Array;
    }

    /// <summary>Builds one page from an object element using the reused writers.</summary>
    /// <param name="element">A JSON object.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="routeWriter">Reused buffer for the route bytes; reset on entry.</param>
    /// <param name="markdownWriter">Reused buffer for the Markdown bytes; reset on entry.</param>
    /// <param name="page">On success, the built page.</param>
    /// <returns><see langword="true"/> when the route template resolved.</returns>
    private static bool TryBuildPage(JsonElement element, ContentMapping mapping, ArrayBufferWriter<byte> routeWriter, ArrayBufferWriter<byte> markdownWriter, out SyntheticPage page)
    {
        routeWriter.ResetWrittenCount();
        if (!TryRenderRoute(mapping.RouteTemplate, element, routeWriter))
        {
            page = default;
            return false;
        }

        markdownWriter.ResetWrittenCount();
        BuildMarkdown(element, mapping, markdownWriter);
        page = new(new Common.FilePath(Encoding.UTF8.GetString(routeWriter.WrittenSpan)), markdownWriter.WrittenSpan.ToArray());
        return true;
    }

    /// <summary>Substitutes <c>{field}</c> placeholders in <paramref name="template"/> from <paramref name="element"/>'s scalar fields.</summary>
    /// <param name="template">Route template bytes.</param>
    /// <param name="element">Source object.</param>
    /// <param name="writer">Destination for the rendered route bytes (already reset by the caller).</param>
    /// <returns><see langword="true"/> when every placeholder resolved to a scalar.</returns>
    private static bool TryRenderRoute(byte[] template, JsonElement element, ArrayBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < template.Length)
        {
            var open = Array.IndexOf(template, (byte)'{', i);
            if (open < 0)
            {
                writer.Write(template.AsSpan(i));
                break;
            }

            writer.Write(template.AsSpan(i, open - i));
            var close = Array.IndexOf(template, (byte)'}', open + 1);
            if (close < 0)
            {
                return false;
            }

            if (!element.TryGetProperty(template.AsSpan(open + 1, close - open - 1), out var value) || !WriteScalar(value, writer))
            {
                return false;
            }

            i = close + 1;
        }

        return true;
    }

    /// <summary>Writes the textual form of a scalar JSON value to <paramref name="writer"/>.</summary>
    /// <param name="value">A JSON value.</param>
    /// <param name="writer">Destination.</param>
    /// <returns><see langword="true"/> when the value is a string, number, or boolean.</returns>
    private static bool WriteScalar(JsonElement value, IBufferWriter<byte> writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
            {
                Encoding.UTF8.GetBytes(value.GetString().AsSpan(), writer);
                return true;
            }

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            {
                Encoding.UTF8.GetBytes(value.GetRawText().AsSpan(), writer);
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Writes the Markdown bytes (frontmatter + body) for one object element into <paramref name="writer"/> (already reset by the caller).</summary>
    /// <param name="element">Source object.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="writer">Destination buffer.</param>
    private static void BuildMarkdown(JsonElement element, ContentMapping mapping, ArrayBufferWriter<byte> writer)
    {
        writer.Write("---\n"u8);

        // foreach over JsonElement.ObjectEnumerator — a struct enumerator with no indexed alternative.
        foreach (var property in element.EnumerateObject())
        {
            if (!IncludeAsFrontmatter(property, mapping))
            {
                continue;
            }

            Encoding.UTF8.GetBytes(property.Name.AsSpan(), writer);
            writer.Write(": "u8);
            Encoding.UTF8.GetBytes(property.Value.GetRawText().AsSpan(), writer);
            writer.Write("\n"u8);
        }

        writer.Write("---\n\n"u8);
        AppendBody(element, mapping, writer);
    }

    /// <summary>True when a property should be emitted into frontmatter.</summary>
    /// <param name="property">A JSON object property.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <returns><see langword="true"/> when the property is not the body key and is allowed by the frontmatter whitelist.</returns>
    private static bool IncludeAsFrontmatter(JsonProperty property, ContentMapping mapping)
    {
        if (mapping.BodyKey is [_, ..] && property.NameEquals(mapping.BodyKey))
        {
            return false;
        }

        if (mapping.FrontmatterKeys is [])
        {
            return true;
        }

        for (var i = 0; i < mapping.FrontmatterKeys.Length; i++)
        {
            if (property.NameEquals(mapping.FrontmatterKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Appends the page body (the body-key field's string value) to <paramref name="writer"/>, ensuring a trailing newline.</summary>
    /// <param name="element">Source object.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="writer">Destination.</param>
    private static void AppendBody(JsonElement element, ContentMapping mapping, ArrayBufferWriter<byte> writer)
    {
        if (mapping.BodyKey is not [_, ..]
            || !element.TryGetProperty(mapping.BodyKey, out var body)
            || body.ValueKind != JsonValueKind.String)
        {
            return;
        }

        Encoding.UTF8.GetBytes(body.GetString().AsSpan(), writer);
        EnsureTrailingNewline(writer);
    }

    /// <summary>Appends a newline to <paramref name="writer"/> unless it already ends with one.</summary>
    /// <param name="writer">Destination.</param>
    private static void EnsureTrailingNewline(ArrayBufferWriter<byte> writer)
    {
        if (writer.WrittenSpan is [.., var last] && last == (byte)'\n')
        {
            return;
        }

        writer.Write("\n"u8);
    }
}
