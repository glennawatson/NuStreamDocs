// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Schemas;

/// <summary>Builds a Markdown reference page from an AsyncAPI 2.x or 3.x JSON specification.</summary>
internal static class AsyncApiPageBuilder
{
    /// <summary>Initial output buffer capacity.</summary>
    private const int InitialBufferCapacity = 1024;

    /// <summary>JSON property key for descriptions.</summary>
    private const string DescriptionKey = "description";

    /// <summary>Gets the Markdown table separator between code-formatted cells.</summary>
    private static ReadOnlySpan<byte> CodeCellSeparator => "` | `"u8;

    /// <summary>Parses <paramref name="specJson"/> and returns the generated <see cref="SyntheticPage"/>.</summary>
    /// <param name="specJson">UTF-8 JSON of the AsyncAPI specification.</param>
    /// <param name="routePrefix">Target subdirectory prefix.</param>
    /// <param name="fallbackName">Fallback title when <c>info.title</c> is absent.</param>
    /// <returns>Single-element page array.</returns>
    /// <exception cref="ContentLoaderException">Thrown when <paramref name="specJson"/> is invalid JSON or not an object.</exception>
    internal static SyntheticPage[] Build(byte[] specJson, PathSegment routePrefix, string fallbackName)
    {
        byte[] title = [];
        byte[] version = [];
        byte[] description = [];
        List<ChannelRow> channels = [];
        List<OperationRow> operations = [];

        try
        {
            var reader = new Utf8JsonReader(specJson);
            SchemaSourceHelper.EnsureRootObject(ref reader, "AsyncAPI root must be a JSON object.");

            while (SchemaSourceHelper.ReadNextProperty(ref reader))
            {
                var key = reader.GetString()!;
                _ = reader.Read();
                if (key == "info" && reader.TokenType == JsonTokenType.StartObject)
                {
                    ReadInfoObject(ref reader, out title, out version, out description);
                }
                else
                {
                    ReadTopLevelSection(ref reader, key, channels, operations);
                }
            }
        }
        catch (JsonException ex)
        {
            throw new ContentLoaderException("AsyncAPI source is not valid JSON.", ex);
        }

        var resolvedTitle = title.Length > 0 ? title : Encoding.UTF8.GetBytes(fallbackName);
        var targetPath = SchemaSourceHelper.BuildTargetPath(routePrefix, Encoding.UTF8.GetString(resolvedTitle));

        var writer = new ArrayBufferWriter<byte>(InitialBufferCapacity);
        SchemaSourceHelper.WriteFrontMatterTitle(writer, resolvedTitle);
        WriteHeaderSummary(writer, version, description);
        WriteChannelsSection(writer, channels);
        WriteOperationsSection(writer, operations);

        return [new(targetPath, writer.WrittenSpan.ToArray())];
    }

    /// <summary>Dispatches top-level <c>channels</c> and <c>operations</c> objects.</summary>
    /// <param name="reader">Reader positioned at the member value.</param>
    /// <param name="key">Property key.</param>
    /// <param name="channels">Parsed channels.</param>
    /// <param name="operations">Parsed operations.</param>
    private static void ReadTopLevelSection(
        ref Utf8JsonReader reader,
        string key,
        List<ChannelRow> channels,
        List<OperationRow> operations)
    {
        if (key == "channels" && reader.TokenType == JsonTokenType.StartObject)
        {
            ReadChannelsMap(ref reader, channels);
        }
        else if (key == "operations" && reader.TokenType == JsonTokenType.StartObject)
        {
            ReadOperationsMap(ref reader, operations);
        }
        else
        {
            reader.Skip();
        }
    }

    /// <summary>Reads <c>info.title</c>, <c>info.version</c>, and <c>info.description</c>.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="title">Title bytes.</param>
    /// <param name="version">Version bytes.</param>
    /// <param name="description">Description bytes.</param>
    private static void ReadInfoObject(
        ref Utf8JsonReader reader,
        out byte[] title,
        out byte[] version,
        out byte[] description)
    {
        title = [];
        version = [];
        description = [];
        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var prop = reader.GetString()!;
            _ = reader.Read();
            if (prop == "title")
            {
                title = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (prop == "version")
            {
                version = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (prop == DescriptionKey)
            {
                description = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    /// <summary>Reads entries of the AsyncAPI <c>channels</c> object.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="channels">Destination list.</param>
    private static void ReadChannelsMap(ref Utf8JsonReader reader, List<ChannelRow> channels)
    {
        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var channelKey = reader.GetString()!;
            _ = reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            channels.Add(ReadSingleChannel(ref reader, channelKey));
        }
    }

    /// <summary>Reads one channel descriptor (<c>address</c>, <c>description</c>, and 2.x <c>publish</c>/<c>subscribe</c> summaries).</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="channelKey">Channel map key.</param>
    /// <returns>Parsed channel row.</returns>
    private static ChannelRow ReadSingleChannel(ref Utf8JsonReader reader, string channelKey)
    {
        byte[] address = [];
        byte[] description = [];
        List<string> directions = [];

        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var prop = reader.GetString()!;
            _ = reader.Read();
            if (prop == "address")
            {
                address = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (prop == DescriptionKey)
            {
                description = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (prop is "publish" or "subscribe")
            {
                directions.Add(prop);
                reader.Skip();
            }
            else
            {
                reader.Skip();
            }
        }

        var topicBytes = address.Length > 0 ? address : Encoding.UTF8.GetBytes(channelKey);
        var modeBytes = directions.Count > 0 ? Encoding.UTF8.GetBytes(string.Join(", ", directions)) : [.. "channel"u8];
        return new(Encoding.UTF8.GetBytes(channelKey), topicBytes, modeBytes, description);
    }

    /// <summary>Reads AsyncAPI 3.x <c>operations</c> entries.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="operations">Destination list.</param>
    private static void ReadOperationsMap(ref Utf8JsonReader reader, List<OperationRow> operations)
    {
        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var operationKey = reader.GetString()!;
            _ = reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            operations.Add(ReadSingleOperation(ref reader, operationKey));
        }
    }

    /// <summary>Reads one AsyncAPI 3.x operation object.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="operationKey">Operation identifier.</param>
    /// <returns>Parsed operation row.</returns>
    private static OperationRow ReadSingleOperation(ref Utf8JsonReader reader, string operationKey)
    {
        byte[] action = [];
        byte[] summary = [];
        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var prop = reader.GetString()!;
            _ = reader.Read();
            if (prop == "action")
            {
                action = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (prop is "summary" or DescriptionKey)
            {
                if (summary.Length == 0)
                {
                    summary = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return new(Encoding.UTF8.GetBytes(operationKey), action, summary);
    }

    /// <summary>Writes version badge and overview prose.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="version">Version bytes.</param>
    /// <param name="description">Description bytes.</param>
    private static void WriteHeaderSummary(IBufferWriter<byte> writer, ReadOnlySpan<byte> version, ReadOnlySpan<byte> description)
    {
        if (!version.IsEmpty)
        {
            writer.Write("**Version:** `"u8);
            writer.Write(version);
            writer.Write("`\n\n"u8);
        }

        if (description.IsEmpty)
        {
            return;
        }

        writer.Write(description);
        writer.Write("\n\n"u8);
    }

    /// <summary>Writes the Markdown Channels table when channels are present.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="channels">Parsed channels.</param>
    private static void WriteChannelsSection(IBufferWriter<byte> writer, List<ChannelRow> channels)
    {
        if (channels.Count == 0)
        {
            return;
        }

        writer.Write("## Channels\n\n| Channel | Address | Direction | Description |\n|---|---|---|---|\n"u8);
        for (var i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            writer.Write("| `"u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, ch.Name);
            writer.Write(CodeCellSeparator);
            SchemaSourceHelper.WriteEscapedTableCell(writer, ch.Address);
            writer.Write(CodeCellSeparator);
            SchemaSourceHelper.WriteEscapedTableCell(writer, ch.Modes);
            writer.Write("` | "u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, ch.Description);
            writer.Write(" |\n"u8);
        }

        writer.Write("\n"u8);
    }

    /// <summary>Writes the Markdown Operations table when AsyncAPI 3.x operations are present.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="operations">Parsed operations.</param>
    private static void WriteOperationsSection(IBufferWriter<byte> writer, List<OperationRow> operations)
    {
        if (operations.Count == 0)
        {
            return;
        }

        writer.Write("## Operations\n\n| Operation | Action | Summary |\n|---|---|---|\n"u8);
        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];
            writer.Write("| `"u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, op.Name);
            writer.Write(CodeCellSeparator);
            SchemaSourceHelper.WriteEscapedTableCell(writer, op.Action.Length > 0 ? op.Action : "send"u8);
            writer.Write("` | "u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, op.Summary);
            writer.Write(" |\n"u8);
        }

        writer.Write("\n"u8);
    }

    /// <summary>Captured AsyncAPI channel metadata.</summary>
    /// <param name="Name">Channel key.</param>
    /// <param name="Address">Topic address.</param>
    /// <param name="Modes">Publish/subscribe direction summary.</param>
    /// <param name="Description">Channel description.</param>
    private readonly record struct ChannelRow(byte[] Name, byte[] Address, byte[] Modes, byte[] Description);

    /// <summary>Captured AsyncAPI 3.x operation metadata.</summary>
    /// <param name="Name">Operation key.</param>
    /// <param name="Action">Send/receive action.</param>
    /// <param name="Summary">Operation summary.</param>
    private readonly record struct OperationRow(byte[] Name, byte[] Action, byte[] Summary);
}
