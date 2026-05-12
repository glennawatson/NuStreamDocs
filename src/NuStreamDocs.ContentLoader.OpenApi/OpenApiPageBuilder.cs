// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.OpenApi;

/// <summary>Renders an OpenAPI 3.x document into one Markdown page per tag.</summary>
internal static class OpenApiPageBuilder
{
    /// <summary>HTTP method names that may appear under a path item.</summary>
    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    /// <summary>Builds the per-tag reference pages from a spec.</summary>
    /// <param name="specJson">UTF-8 JSON of the OpenAPI document (YAML callers convert first).</param>
    /// <param name="routePrefix">Local subdirectory the pages are placed under.</param>
    /// <returns>One page per tag that has at least one operation.</returns>
    /// <exception cref="ContentLoaderException">When the spec is not valid JSON.</exception>
    public static SyntheticPage[] Build(byte[] specJson, ReadOnlySpan<byte> routePrefix)
    {
        var routeBase = routePrefix.IsEmpty ? string.Empty : Encoding.UTF8.GetString(routePrefix).TrimEnd('/');
        using var document = ParseOrThrow(specJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("paths"u8, out var paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        Dictionary<string, StringBuilder> sections = new(StringComparer.Ordinal);
        CollectOperations(paths, sections);
        return EmitPages(sections, routeBase);
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
            throw new ContentLoaderException("OpenAPI source is not valid JSON.", ex);
        }
    }

    /// <summary>Walks every path/operation in the spec and appends each operation's Markdown to its tag's section.</summary>
    /// <param name="paths">The <c>paths</c> object.</param>
    /// <param name="sections">Tag name to accumulated Markdown.</param>
    private static void CollectOperations(JsonElement paths, Dictionary<string, StringBuilder> sections)
    {
        // foreach over JsonElement.ObjectEnumerator — a struct enumerator with no indexed alternative.
        foreach (var pathItem in paths.EnumerateObject())
        {
            if (pathItem.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var methodItem in pathItem.Value.EnumerateObject())
            {
                if (!IsHttpMethod(methodItem.Name) || methodItem.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var tag = FirstTag(methodItem.Value);
                if (!sections.TryGetValue(tag, out var builder))
                {
                    builder = new();
                    sections[tag] = builder;
                }

                AppendOperation(builder, methodItem.Name, pathItem.Name, methodItem.Value);
            }
        }
    }

    /// <summary>Emits one page per non-empty tag section.</summary>
    /// <param name="sections">Tag name to accumulated Markdown.</param>
    /// <param name="routeBase">Route prefix without a trailing slash, or empty.</param>
    /// <returns>The pages.</returns>
    private static SyntheticPage[] EmitPages(Dictionary<string, StringBuilder> sections, string routeBase)
    {
        var pages = new SyntheticPage[sections.Count];
        var index = 0;

        // foreach over Dictionary<string, StringBuilder> — a struct enumerator with no indexed alternative.
        foreach (var (tag, body) in sections)
        {
            var slug = Slugify(tag);
            var route = routeBase.Length > 0 ? routeBase + "/" + slug + ".md" : slug + ".md";
            StringBuilder page = new(body.Length + tag.Length + 64);
            page.Append("---\ntitle: \"").Append(EscapeYaml(tag)).Append("\"\n---\n\n# ").Append(tag).Append("\n\n").Append(body);
            pages[index++] = new(new FilePath(route), Encoding.UTF8.GetBytes(page.ToString()));
        }

        return pages;
    }

    /// <summary>Appends one operation's Markdown — heading, summary/description, parameters table, request body, responses.</summary>
    /// <param name="builder">Destination section.</param>
    /// <param name="method">HTTP method name (lowercase as it appeared).</param>
    /// <param name="path">Path template.</param>
    /// <param name="operation">The operation object.</param>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Linear rendering of OpenAPI operation sections; the branching tracks which optional sections are present, not nested logic.")]
    [SuppressMessage(
        "Sonar Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "Linear rendering of OpenAPI operation sections; the branching tracks which optional sections are present, not nested logic.")]
    private static void AppendOperation(StringBuilder builder, string method, string path, JsonElement operation)
    {
        builder.Append("## ").Append(method.ToUpperInvariant()).Append(' ').Append(path).Append("\n\n");
        AppendIfText(builder, operation, "summary"u8, "\n\n");
        AppendIfText(builder, operation, "description"u8, "\n\n");
        AppendParameters(builder, operation);
        AppendRequestBody(builder, operation);
        AppendResponses(builder, operation);
    }

    /// <summary>Appends the parameters table when the operation has parameters.</summary>
    /// <param name="builder">Destination.</param>
    /// <param name="operation">The operation object.</param>
    private static void AppendParameters(StringBuilder builder, JsonElement operation)
    {
        if (!operation.TryGetProperty("parameters"u8, out var parameters) || parameters.ValueKind != JsonValueKind.Array || parameters.GetArrayLength() == 0)
        {
            return;
        }

        builder.Append("| Name | In | Type | Required | Description |\n|---|---|---|---|---|\n");

        // foreach over JsonElement.ArrayEnumerator — a struct enumerator with no indexed alternative.
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = parameter.TryGetProperty("schema"u8, out var schema) ? Text(schema, "type"u8) : string.Empty;
            var required = parameter.TryGetProperty("required"u8, out var req) && req.ValueKind == JsonValueKind.True ? "yes" : "no";
            builder.Append("| ").Append(Cell(Text(parameter, "name"u8)))
                .Append(" | ").Append(Cell(Text(parameter, "in"u8)))
                .Append(" | ").Append(Cell(type))
                .Append(" | ").Append(required)
                .Append(" | ").Append(Cell(Text(parameter, "description"u8)))
                .Append(" |\n");
        }

        builder.Append('\n');
    }

    /// <summary>Appends the request-body content-type list when present.</summary>
    /// <param name="builder">Destination.</param>
    /// <param name="operation">The operation object.</param>
    private static void AppendRequestBody(StringBuilder builder, JsonElement operation)
    {
        if (!operation.TryGetProperty("requestBody"u8, out var requestBody)
            || !requestBody.TryGetProperty("content"u8, out var content)
            || content.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        builder.Append("**Request body:** ");
        var first = true;
        foreach (var mediaType in content.EnumerateObject())
        {
            if (!first)
            {
                builder.Append(", ");
            }

            builder.Append('`').Append(mediaType.Name).Append('`');
            first = false;
        }

        builder.Append("\n\n");
    }

    /// <summary>Appends the response list when present.</summary>
    /// <param name="builder">Destination.</param>
    /// <param name="operation">The operation object.</param>
    private static void AppendResponses(StringBuilder builder, JsonElement operation)
    {
        if (!operation.TryGetProperty("responses"u8, out var responses) || responses.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        builder.Append("**Responses:**\n\n");
        foreach (var response in responses.EnumerateObject())
        {
            var description = response.Value.ValueKind == JsonValueKind.Object ? Text(response.Value, "description"u8) : string.Empty;
            builder.Append("- `").Append(response.Name).Append("` — ").Append(Cell(description)).Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>Appends a string property's value followed by <paramref name="suffix"/> when present and non-empty.</summary>
    /// <param name="builder">Destination.</param>
    /// <param name="element">Source object.</param>
    /// <param name="key">Property name.</param>
    /// <param name="suffix">Text appended after the value.</param>
    private static void AppendIfText(StringBuilder builder, JsonElement element, ReadOnlySpan<byte> key, string suffix)
    {
        var value = Text(element, key);
        if (value.Length == 0)
        {
            return;
        }

        builder.Append(value).Append(suffix);
    }

    /// <summary>Returns the string value of <paramref name="key"/> on <paramref name="element"/>, or an empty string.</summary>
    /// <param name="element">Source object.</param>
    /// <param name="key">Property name.</param>
    /// <returns>The value text.</returns>
    private static string Text(JsonElement element, ReadOnlySpan<byte> key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>Makes <paramref name="text"/> safe to place inside a Markdown table cell.</summary>
    /// <param name="text">Source text.</param>
    /// <returns>The escaped text.</returns>
    private static string Cell(string text) =>
        text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

    /// <summary>Escapes <paramref name="text"/> for a double-quoted YAML scalar.</summary>
    /// <param name="text">Source text.</param>
    /// <returns>The escaped text.</returns>
    private static string EscapeYaml(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).ReplaceLineEndings(" ");

    /// <summary>Returns the first tag of an operation, or <c>"default"</c> when it has none.</summary>
    /// <param name="operation">The operation object.</param>
    /// <returns>The tag name.</returns>
    private static string FirstTag(JsonElement operation)
    {
        if (operation.TryGetProperty("tags"u8, out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tags.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String && tag.GetString() is { Length: > 0 } value)
                {
                    return value;
                }
            }
        }

        return "default";
    }

    /// <summary>True when <paramref name="name"/> is an HTTP method that may appear under a path item.</summary>
    /// <param name="name">Candidate property name.</param>
    /// <returns><see langword="true"/> for a known method.</returns>
    private static bool IsHttpMethod(string name)
    {
        for (var i = 0; i < HttpMethods.Length; i++)
        {
            if (string.Equals(HttpMethods[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lowercases ASCII letters/digits and collapses every other run into a single hyphen.</summary>
    /// <param name="text">Source text.</param>
    /// <returns>A non-empty slug.</returns>
    private static string Slugify(string text)
    {
        StringBuilder builder = new(text.Length);
        var lastWasHyphen = true;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        if (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.Length == 0 ? "default" : builder.ToString();
    }
}
