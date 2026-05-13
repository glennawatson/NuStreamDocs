// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.OpenApi;

/// <summary>
/// Streams an OpenAPI 3.x document into one Markdown page per tag with a forward-only
/// <see cref="Utf8JsonReader"/>, building straight into reused <see cref="ArrayBufferWriter{T}"/>
/// buffers — byte-first, no document model. Each operation's sections are buffered as they stream
/// and re-emitted in the fixed heading → summary → description → parameters → request body →
/// responses order.
/// </summary>
internal static class OpenApiPageBuilder
{
    /// <summary>Initial byte capacity for a per-operation buffer.</summary>
    private const int OperationCapacity = 512;

    /// <summary>HTTP method property names that introduce an operation under a path item.</summary>
    private static readonly byte[][] HttpMethods =
    [
        [.. "get"u8], [.. "put"u8], [.. "post"u8], [.. "delete"u8],
        [.. "options"u8], [.. "head"u8], [.. "patch"u8], [.. "trace"u8]
    ];

    /// <summary>Gets the bytes that must be escaped when written inside a double-quoted YAML scalar.</summary>
    private static ReadOnlySpan<byte> YamlSpecials => "\"\\\n\r"u8;

    /// <summary>Builds the per-tag reference pages from a spec.</summary>
    /// <param name="specJson">UTF-8 JSON of the OpenAPI document (YAML callers convert first).</param>
    /// <param name="routePrefix">Local subdirectory the pages are placed under.</param>
    /// <returns>One page per tag that has at least one operation.</returns>
    /// <exception cref="ContentLoaderException">When the spec is not valid JSON.</exception>
    public static SyntheticPage[] Build(byte[] specJson, ReadOnlySpan<byte> routePrefix)
    {
        ArgumentNullException.ThrowIfNull(specJson);
        var routeBase = NormalizeRouteBase(routePrefix);

        RenderState state = new();
        try
        {
            Utf8JsonReader reader = new(specJson);
            if (!AdvanceToObjectProperty(ref reader, "paths"u8))
            {
                return [];
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var pathBytes = reader.ValueSpan;
                if (!reader.Read())
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                ReadPathItem(ref reader, pathBytes, state);
            }
        }
        catch (JsonException ex)
        {
            throw new ContentLoaderException("OpenAPI source is not valid JSON.", ex);
        }

        return EmitPages(state, routeBase);
    }

    /// <summary>Advances <paramref name="reader"/> to the value of the root object's property named <paramref name="name"/>, requiring it to be an object.</summary>
    /// <param name="reader">Reader at the document start.</param>
    /// <param name="name">Property name to find.</param>
    /// <returns><see langword="true"/> when found and the value is a JSON object.</returns>
    private static bool AdvanceToObjectProperty(ref Utf8JsonReader reader, ReadOnlySpan<byte> name)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var match = reader.ValueTextEquals(name);
            if (!reader.Read())
            {
                return false;
            }

            if (match)
            {
                return reader.TokenType == JsonTokenType.StartObject;
            }

            reader.Skip();
        }

        return false;
    }

    /// <summary>Reads a path-item object, dispatching each HTTP-method property to <see cref="ReadOperation"/>.</summary>
    /// <param name="reader">Reader at the path-item's <c>{</c>.</param>
    /// <param name="path">The path template bytes (a slice of the spec).</param>
    /// <param name="state">Reused render state.</param>
    private static void ReadPathItem(ref Utf8JsonReader reader, scoped ReadOnlySpan<byte> path, RenderState state)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isMethod = IsHttpMethod(ref reader);
            var method = reader.ValueSpan;
            if (!reader.Read())
            {
                break;
            }

            if (!isMethod || reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            ReadOperation(ref reader, path, method, state);
        }
    }

    /// <summary>Reads one operation object — buffering its sections — and routes the rendered Markdown to its tag's page.</summary>
    /// <param name="reader">Reader at the operation's <c>{</c>.</param>
    /// <param name="path">The path template bytes.</param>
    /// <param name="method">The HTTP method name bytes (lowercase as in the spec).</param>
    /// <param name="state">Reused render state.</param>
    private static void ReadOperation(
        ref Utf8JsonReader reader,
        scoped ReadOnlySpan<byte> path,
        scoped ReadOnlySpan<byte> method,
        RenderState state)
    {
        state.ResetOperation();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("tags"u8))
            {
                AdvanceToValue(ref reader);
                ReadFirstTag(ref reader, state.Tag);
            }
            else if (reader.ValueTextEquals("summary"u8))
            {
                AdvanceToValue(ref reader);
                CopyStringValue(ref reader, state.Summary);
            }
            else if (reader.ValueTextEquals("description"u8))
            {
                AdvanceToValue(ref reader);
                CopyStringValue(ref reader, state.Description);
            }
            else if (reader.ValueTextEquals("parameters"u8))
            {
                AdvanceToValue(ref reader);
                ReadParameters(ref reader, state);
            }
            else if (reader.ValueTextEquals("requestBody"u8))
            {
                AdvanceToValue(ref reader);
                ReadRequestBody(ref reader, state);
            }
            else if (reader.ValueTextEquals("responses"u8))
            {
                AdvanceToValue(ref reader);
                ReadResponses(ref reader, state);
            }
            else
            {
                AdvanceToValue(ref reader);
                reader.Skip();
            }
        }

        AssembleOperation(state, path, method);
    }

    /// <summary>Re-emits the buffered operation sections in fixed order and appends the result to the tag's page.</summary>
    /// <param name="state">Render state holding the buffered sections.</param>
    /// <param name="path">The path template bytes.</param>
    /// <param name="method">The HTTP method name bytes.</param>
    private static void AssembleOperation(
        RenderState state,
        scoped ReadOnlySpan<byte> path,
        scoped ReadOnlySpan<byte> method)
    {
        var operation = state.Operation;
        operation.ResetWrittenCount();
        operation.Write("## "u8);
        WriteUpperAscii(method, operation);
        operation.Write(" "u8);
        operation.Write(path);
        operation.Write("\n\n"u8);
        AppendBlock(operation, state.Summary, "\n\n"u8);
        AppendBlock(operation, state.Description, "\n\n"u8);
        operation.Write(state.Parameters.WrittenSpan);
        operation.Write(state.RequestBody.WrittenSpan);
        operation.Write(state.Responses.WrittenSpan);

        var tag = state.Tag.WrittenCount > 0 ? state.Tag.WrittenSpan : "default"u8;
        var lookup = state.TagPages.GetAlternateLookup<ReadOnlySpan<byte>>();
        if (!lookup.TryGetValue(tag, out var page))
        {
            page = new();
            state.TagPages[tag.ToArray()] = page;
        }

        page.Write(operation.WrittenSpan);
    }

    /// <summary>Emits one page per tag.</summary>
    /// <param name="state">Render state holding the per-tag pages.</param>
    /// <param name="routeBase">Route prefix bytes without a trailing slash, or empty.</param>
    /// <returns>The pages.</returns>
    private static SyntheticPage[] EmitPages(RenderState state, byte[] routeBase)
    {
        var pages = new SyntheticPage[state.TagPages.Count];
        var index = 0;

        // foreach over Dictionary<byte[], ArrayBufferWriter<byte>> — a struct enumerator with no indexed alternative.
        foreach (var (tagBytes, body) in state.TagPages)
        {
            ArrayBufferWriter<byte> markdown = new(body.WrittenCount + tagBytes.Length + 64);
            markdown.Write("---\ntitle: \""u8);
            WriteEscapedYaml(tagBytes, markdown);
            markdown.Write("\"\n---\n\n# "u8);
            markdown.Write(tagBytes);
            markdown.Write("\n\n"u8);
            markdown.Write(body.WrittenSpan);

            var route = ComposeRoute(routeBase, Slugify(tagBytes));
            pages[index++] = new(new(Encoding.UTF8.GetString(route)), markdown.WrittenSpan.ToArray());
        }

        return pages;
    }

    /// <summary>Reads the parameters array into the parameters-table buffer (header first, then a row per parameter).</summary>
    /// <param name="reader">Reader at the value of <c>parameters</c>.</param>
    /// <param name="state">Reused render state.</param>
    private static void ReadParameters(ref Utf8JsonReader reader, RenderState state)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            if (!state.ParameterHeaderWritten)
            {
                state.Parameters.Write("| Name | In | Type | Required | Description |\n|---|---|---|---|---|\n"u8);
                state.ParameterHeaderWritten = true;
            }

            ReadParameterRow(ref reader, state);
        }

        if (!state.ParameterHeaderWritten)
        {
            return;
        }

        state.Parameters.Write("\n"u8);
    }

    /// <summary>Reads one parameter object and appends its table row.</summary>
    /// <param name="reader">Reader at the parameter's <c>{</c>.</param>
    /// <param name="state">Reused render state.</param>
    private static void ReadParameterRow(ref Utf8JsonReader reader, RenderState state)
    {
        state.ResetParameterCells();
        var required = false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("name"u8))
            {
                AdvanceToValue(ref reader);
                CopyStringValue(ref reader, state.CellName);
            }
            else if (reader.ValueTextEquals("in"u8))
            {
                AdvanceToValue(ref reader);
                CopyStringValue(ref reader, state.CellIn);
            }
            else if (reader.ValueTextEquals("description"u8))
            {
                AdvanceToValue(ref reader);
                CopyStringValue(ref reader, state.CellDescription);
            }
            else if (reader.ValueTextEquals("required"u8))
            {
                AdvanceToValue(ref reader);
                required = reader.TokenType == JsonTokenType.True;
            }
            else if (reader.ValueTextEquals("schema"u8))
            {
                AdvanceToValue(ref reader);
                ReadSchemaType(ref reader, state.CellType);
            }
            else
            {
                AdvanceToValue(ref reader);
                reader.Skip();
            }
        }

        var row = state.Parameters;
        row.Write("| "u8);
        WriteSanitizedCell(state.CellName.WrittenSpan, row);
        row.Write(" | "u8);
        WriteSanitizedCell(state.CellIn.WrittenSpan, row);
        row.Write(" | "u8);
        WriteSanitizedCell(state.CellType.WrittenSpan, row);
        row.Write(required ? " | yes | "u8 : " | no | "u8);
        WriteSanitizedCell(state.CellDescription.WrittenSpan, row);
        row.Write(" |\n"u8);
    }

    /// <summary>Reads a schema object, capturing its <c>type</c> into <paramref name="typeBuffer"/>.</summary>
    /// <param name="reader">Reader at the value of <c>schema</c>.</param>
    /// <param name="typeBuffer">Destination for the type bytes.</param>
    private static void ReadSchemaType(ref Utf8JsonReader reader, ArrayBufferWriter<byte> typeBuffer)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("type"u8))
            {
                AdvanceToValue(ref reader);
                CopyStringValue(ref reader, typeBuffer);
            }
            else
            {
                AdvanceToValue(ref reader);
                reader.Skip();
            }
        }
    }

    /// <summary>Reads a request-body object, emitting a comma-separated list of its content media types when present.</summary>
    /// <param name="reader">Reader at the value of <c>requestBody</c>.</param>
    /// <param name="state">Reused render state.</param>
    private static void ReadRequestBody(ref Utf8JsonReader reader, RenderState state)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("content"u8))
            {
                AdvanceToValue(ref reader);
                ReadMediaTypes(ref reader, state);
            }
            else
            {
                AdvanceToValue(ref reader);
                reader.Skip();
            }
        }
    }

    /// <summary>Reads a content object — its property names are media types — and writes <c>**Request body:** ...</c> when there is at least one.</summary>
    /// <param name="reader">Reader at the value of <c>content</c>.</param>
    /// <param name="state">Reused render state.</param>
    private static void ReadMediaTypes(ref Utf8JsonReader reader, RenderState state)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        state.Scratch.ResetWrittenCount();
        var first = true;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (!first)
            {
                state.Scratch.Write(", "u8);
            }

            state.Scratch.Write("`"u8);
            CopyToken(ref reader, state.Scratch);
            state.Scratch.Write("`"u8);
            first = false;
            AdvanceToValue(ref reader);
            reader.Skip();
        }

        if (state.Scratch.WrittenCount == 0)
        {
            return;
        }

        state.RequestBody.Write("**Request body:** "u8);
        state.RequestBody.Write(state.Scratch.WrittenSpan);
        state.RequestBody.Write("\n\n"u8);
    }

    /// <summary>Reads a responses object — its property names are status codes — into the responses buffer.</summary>
    /// <param name="reader">Reader at the value of <c>responses</c>.</param>
    /// <param name="state">Reused render state.</param>
    private static void ReadResponses(ref Utf8JsonReader reader, RenderState state)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        state.Responses.Write("**Responses:**\n\n"u8);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            state.Responses.Write("- `"u8);
            CopyToken(ref reader, state.Responses);
            state.Responses.Write("` — "u8);
            AdvanceToValue(ref reader);
            WriteResponseDescription(ref reader, state);
            state.Responses.Write("\n"u8);
        }

        state.Responses.Write("\n"u8);
    }

    /// <summary>Writes a single response's <c>description</c> (sanitized for a one-line cell) into the responses buffer.</summary>
    /// <param name="reader">Reader at the value of a status-code property.</param>
    /// <param name="state">Reused render state.</param>
    private static void WriteResponseDescription(ref Utf8JsonReader reader, RenderState state)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        state.Scratch.ResetWrittenCount();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("description"u8))
            {
                AdvanceToValue(ref reader);
                CopyStringValue(ref reader, state.Scratch);
            }
            else
            {
                AdvanceToValue(ref reader);
                reader.Skip();
            }
        }

        WriteSanitizedCell(state.Scratch.WrittenSpan, state.Responses);
    }

    /// <summary>Captures the first non-empty string element of a <c>tags</c> array.</summary>
    /// <param name="reader">Reader at the value of <c>tags</c>.</param>
    /// <param name="tagBuffer">Destination for the tag bytes.</param>
    private static void ReadFirstTag(ref Utf8JsonReader reader, ArrayBufferWriter<byte> tagBuffer)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (tagBuffer.WrittenCount == 0 && reader.TokenType == JsonTokenType.String)
            {
                CopyToken(ref reader, tagBuffer);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    /// <summary>Advances the reader to a property's value; the caller is positioned on the property name.</summary>
    /// <param name="reader">Reader on a property name.</param>
    private static void AdvanceToValue(ref Utf8JsonReader reader) => reader.Read();

    /// <summary>True when the property the reader is on is an HTTP method that introduces an operation.</summary>
    /// <param name="reader">Reader on a property name.</param>
    /// <returns><see langword="true"/> for a known method.</returns>
    private static bool IsHttpMethod(scoped ref Utf8JsonReader reader)
    {
        for (var i = 0; i < HttpMethods.Length; i++)
        {
            if (reader.ValueTextEquals(HttpMethods[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Copies the unescaped UTF-8 bytes of the current string token into <paramref name="destination"/>; no-op for a non-string value.</summary>
    /// <param name="reader">Reader on a value token.</param>
    /// <param name="destination">Destination.</param>
    private static void CopyStringValue(scoped ref Utf8JsonReader reader, ArrayBufferWriter<byte> destination)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return;
        }

        CopyToken(ref reader, destination);
    }

    /// <summary>Copies the unescaped UTF-8 bytes of the current string token or property name into <paramref name="destination"/>.</summary>
    /// <param name="reader">Reader on a string token or property name.</param>
    /// <param name="destination">Destination.</param>
    private static void CopyToken(scoped ref Utf8JsonReader reader, ArrayBufferWriter<byte> destination)
    {
        var span = destination.GetSpan(reader.ValueSpan.Length);
        var written = reader.CopyString(span);
        destination.Advance(written);
    }

    /// <summary>Appends <paramref name="block"/> followed by <paramref name="suffix"/> to <paramref name="destination"/> when the block is non-empty.</summary>
    /// <param name="destination">Destination.</param>
    /// <param name="block">A buffered section.</param>
    /// <param name="suffix">Trailing bytes.</param>
    private static void AppendBlock(
        ArrayBufferWriter<byte> destination,
        ArrayBufferWriter<byte> block,
        ReadOnlySpan<byte> suffix)
    {
        if (block.WrittenCount == 0)
        {
            return;
        }

        destination.Write(block.WrittenSpan);
        destination.Write(suffix);
    }

    /// <summary>Copies <paramref name="source"/> into a Markdown table cell — pipes escaped, line breaks collapsed to spaces.</summary>
    /// <param name="source">Cell text bytes.</param>
    /// <param name="destination">Destination.</param>
    private static void WriteSanitizedCell(ReadOnlySpan<byte> source, ArrayBufferWriter<byte> destination)
    {
        var rest = source;
        while (!rest.IsEmpty)
        {
            var index = rest.IndexOfAny((byte)'|', (byte)'\n', (byte)'\r');
            if (index < 0)
            {
                destination.Write(rest);
                return;
            }

            destination.Write(rest[..index]);
            destination.Write(rest[index] == (byte)'|' ? "\\|"u8 : " "u8);
            rest = rest[(index + 1)..];
        }
    }

    /// <summary>Copies <paramref name="source"/> into a double-quoted YAML scalar — quotes and backslashes escaped, line breaks collapsed.</summary>
    /// <param name="source">Scalar text bytes.</param>
    /// <param name="destination">Destination.</param>
    private static void WriteEscapedYaml(ReadOnlySpan<byte> source, ArrayBufferWriter<byte> destination)
    {
        var rest = source;
        while (!rest.IsEmpty)
        {
            var index = rest.IndexOfAny(YamlSpecials);
            if (index < 0)
            {
                destination.Write(rest);
                return;
            }

            destination.Write(rest[..index]);
            destination.Write(EscapeFor(rest[index]));
            rest = rest[(index + 1)..];
        }
    }

    /// <summary>Returns the YAML escape sequence for a special byte.</summary>
    /// <param name="b">A byte from <see cref="YamlSpecials"/>.</param>
    /// <returns>The escape sequence bytes.</returns>
    private static ReadOnlySpan<byte> EscapeFor(byte b) => b switch
    {
        (byte)'"' => "\\\""u8,
        (byte)'\\' => "\\\\"u8,
        _ => " "u8
    };

    /// <summary>Writes <paramref name="source"/> to <paramref name="destination"/> with ASCII letters uppercased.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="destination">Destination.</param>
    private static void WriteUpperAscii(scoped ReadOnlySpan<byte> source, ArrayBufferWriter<byte> destination)
    {
        var span = destination.GetSpan(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var b = source[i];
            span[i] = b is >= (byte)'a' and <= (byte)'z' ? (byte)(b - AsciiByteHelpers.AsciiCaseBit) : b;
        }

        destination.Advance(source.Length);
    }

    /// <summary>Normalizes the route prefix to bytes without a trailing slash (empty stays empty).</summary>
    /// <param name="routePrefix">Configured route prefix.</param>
    /// <returns>The prefix bytes.</returns>
    private static byte[] NormalizeRouteBase(ReadOnlySpan<byte> routePrefix)
    {
        if (routePrefix.IsEmpty)
        {
            return [];
        }

        return (routePrefix[^1] == (byte)'/' ? routePrefix[..^1] : routePrefix).ToArray();
    }

    /// <summary>Composes <c>{prefix}/{slug}.md</c> bytes (or <c>{slug}.md</c> when the prefix is empty).</summary>
    /// <param name="routeBase">Route prefix bytes without a trailing slash, or empty.</param>
    /// <param name="slug">The slugified tag bytes.</param>
    /// <returns>The route bytes.</returns>
    private static byte[] ComposeRoute(ReadOnlySpan<byte> routeBase, ReadOnlySpan<byte> slug) =>
        routeBase.Length > 0 ? [.. routeBase, (byte)'/', .. slug, .. ".md"u8] : [.. slug, .. ".md"u8];

    /// <summary>Lowercases ASCII letters/digits and collapses every other run into a single hyphen.</summary>
    /// <param name="source">Source text bytes.</param>
    /// <returns>A non-empty slug.</returns>
    private static byte[] Slugify(ReadOnlySpan<byte> source)
    {
        var buffer = new byte[source.Length + 1];
        var length = 0;
        var lastWasHyphen = true;
        for (var i = 0; i < source.Length; i++)
        {
            var b = source[i];
            if (AsciiByteHelpers.IsAsciiLetter(b) || AsciiByteHelpers.IsAsciiDigit(b))
            {
                buffer[length++] = AsciiByteHelpers.ToAsciiLowerByte(b);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                buffer[length++] = (byte)'-';
                lastWasHyphen = true;
            }
        }

        if (length > 0 && buffer[length - 1] == (byte)'-')
        {
            length--;
        }

        return length == 0 ? [.. "default"u8] : buffer[..length];
    }

    /// <summary>Reused buffers and per-tag pages for a single <see cref="Build"/> call.</summary>
    private sealed class RenderState
    {
        /// <summary>Gets the accumulated Markdown body per tag, byte-keyed.</summary>
        public Dictionary<byte[], ArrayBufferWriter<byte>> TagPages { get; } = new(ByteArrayComparer.Instance);

        /// <summary>Gets the buffer the current operation is assembled into before being routed to its tag.</summary>
        public ArrayBufferWriter<byte> Operation { get; } = new(OperationCapacity);

        /// <summary>Gets the current operation's first tag bytes.</summary>
        public ArrayBufferWriter<byte> Tag { get; } = new();

        /// <summary>Gets the current operation's summary bytes.</summary>
        public ArrayBufferWriter<byte> Summary { get; } = new();

        /// <summary>Gets the current operation's description bytes.</summary>
        public ArrayBufferWriter<byte> Description { get; } = new();

        /// <summary>Gets the current operation's parameters-table bytes (header + rows + trailing newline, or empty).</summary>
        public ArrayBufferWriter<byte> Parameters { get; } = new();

        /// <summary>Gets the current operation's request-body section bytes (or empty).</summary>
        public ArrayBufferWriter<byte> RequestBody { get; } = new();

        /// <summary>Gets the current operation's responses section bytes (or empty).</summary>
        public ArrayBufferWriter<byte> Responses { get; } = new();

        /// <summary>Gets the current parameter row's name cell bytes.</summary>
        public ArrayBufferWriter<byte> CellName { get; } = new();

        /// <summary>Gets the current parameter row's location cell bytes.</summary>
        public ArrayBufferWriter<byte> CellIn { get; } = new();

        /// <summary>Gets the current parameter row's type cell bytes.</summary>
        public ArrayBufferWriter<byte> CellType { get; } = new();

        /// <summary>Gets the current parameter row's description cell bytes.</summary>
        public ArrayBufferWriter<byte> CellDescription { get; } = new();

        /// <summary>Gets a scratch buffer for transient byte assembly (media-type lists, response descriptions).</summary>
        public ArrayBufferWriter<byte> Scratch { get; } = new();

        /// <summary>Gets or sets a value indicating whether the parameters-table header has been written for the current operation.</summary>
        public bool ParameterHeaderWritten { get; set; }

        /// <summary>Clears the per-operation buffers and flags.</summary>
        public void ResetOperation()
        {
            Tag.ResetWrittenCount();
            Summary.ResetWrittenCount();
            Description.ResetWrittenCount();
            Parameters.ResetWrittenCount();
            RequestBody.ResetWrittenCount();
            Responses.ResetWrittenCount();
            ParameterHeaderWritten = false;
        }

        /// <summary>Clears the per-parameter cell buffers.</summary>
        public void ResetParameterCells()
        {
            CellName.ResetWrittenCount();
            CellIn.ResetWrittenCount();
            CellType.ResetWrittenCount();
            CellDescription.ResetWrittenCount();
        }
    }
}
