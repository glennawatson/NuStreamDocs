// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Schemas;

/// <summary>Builds a Markdown reference page from a JSON Schema (Draft-07 / 2020-12) document using <see cref="Utf8JsonReader"/>.</summary>
internal static class JsonSchemaPageBuilder
{
    /// <summary>Initial output buffer capacity.</summary>
    private const int InitialBufferCapacity = 1024;

    /// <summary>Parses <paramref name="schemaJson"/> and returns the generated <see cref="SyntheticPage"/>.</summary>
    /// <param name="schemaJson">UTF-8 JSON Schema bytes.</param>
    /// <param name="routePrefix">Target subdirectory prefix.</param>
    /// <param name="fallbackName">Fallback page name when the schema declares no <c>title</c>.</param>
    /// <returns>Single-element page array.</returns>
    /// <exception cref="ContentLoaderException">Thrown when <paramref name="schemaJson"/> is invalid JSON or not a JSON object.</exception>
    internal static SyntheticPage[] Build(byte[] schemaJson, PathSegment routePrefix, string fallbackName)
    {
        SchemaObjectModel root;
        Dictionary<string, SchemaObjectModel> defs = [with(StringComparer.Ordinal)];
        try
        {
            var reader = new Utf8JsonReader(schemaJson);
            SchemaSourceHelper.EnsureRootObject(ref reader, "JSON Schema root must be a JSON object.");
            root = ReadSchemaObject(ref reader, defs);
        }
        catch (JsonException ex)
        {
            throw new ContentLoaderException("JSON Schema source is not valid JSON.", ex);
        }

        var titleBytes = root.Title.Length > 0 ? root.Title : Encoding.UTF8.GetBytes(fallbackName);
        var slugSource = Encoding.UTF8.GetString(titleBytes);
        var targetPath = SchemaSourceHelper.BuildTargetPath(routePrefix, slugSource);

        var writer = new ArrayBufferWriter<byte>(InitialBufferCapacity);
        SchemaSourceHelper.WriteFrontMatterTitle(writer, titleBytes);
        WriteSchemaBody(writer, root);

        foreach (var def in defs)
        {
            writer.Write("## `"u8);
            writer.Write(Encoding.UTF8.GetBytes(def.Key));
            writer.Write("`\n\n"u8);
            WriteSchemaBody(writer, def.Value);
        }

        return [new(targetPath, writer.WrittenSpan.ToArray())];
    }

    /// <summary>Reads one JSON Schema object (and optionally top-level <c>$defs</c>/<c>definitions</c>).</summary>
    /// <param name="reader">Reader positioned at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="defs">Optional dictionary populated with sub-schemas.</param>
    /// <returns>Parsed <see cref="SchemaObjectModel"/>.</returns>
    private static SchemaObjectModel ReadSchemaObject(
        ref Utf8JsonReader reader,
        Dictionary<string, SchemaObjectModel>? defs)
    {
        byte[] title = [];
        byte[] description = [];
        byte[] schemaType = [];
        HashSet<string> required = [with(StringComparer.Ordinal)];
        List<SchemaPropertyRow> properties = [];

        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var propName = reader.GetString()!;
            _ = reader.Read();
            if (propName == "title")
            {
                title = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (propName == "description")
            {
                description = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (propName is "type" or "$ref")
            {
                schemaType = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else
            {
                ReadCompositeSchemaMember(ref reader, propName, required, properties, defs);
            }
        }

        return new(title, description, schemaType, required, [.. properties]);
    }

    /// <summary>Dispatches composite JSON Schema keywords (<c>required</c>, <c>properties</c>, <c>$defs</c>/<c>definitions</c>).</summary>
    /// <param name="reader">Reader positioned at the member value.</param>
    /// <param name="propName">Member name.</param>
    /// <param name="required">Required property names.</param>
    /// <param name="properties">Parsed property descriptors.</param>
    /// <param name="defs">Optional sub-schema dictionary.</param>
    private static void ReadCompositeSchemaMember(
        ref Utf8JsonReader reader,
        string propName,
        HashSet<string> required,
        List<SchemaPropertyRow> properties,
        Dictionary<string, SchemaObjectModel>? defs)
    {
        if (propName == "required" && reader.TokenType == JsonTokenType.StartArray)
        {
            ReadRequiredSet(ref reader, required);
        }
        else if (propName == "properties" && reader.TokenType == JsonTokenType.StartObject)
        {
            ReadPropertiesMap(ref reader, properties);
        }
        else if (defs is not null && propName is "$defs" or "definitions" && reader.TokenType == JsonTokenType.StartObject)
        {
            ReadDefinitionsMap(ref reader, defs);
        }
        else
        {
            reader.Skip();
        }
    }

    /// <summary>Reads strings from a JSON Schema <c>required</c> array.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartArray"/>.</param>
    /// <param name="required">Destination set.</param>
    private static void ReadRequiredSet(ref Utf8JsonReader reader, HashSet<string> required)
    {
        while (SchemaSourceHelper.ReadNextElement(ref reader))
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                _ = required.Add(reader.GetString()!);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    /// <summary>Reads entries of a JSON Schema <c>properties</c> object.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="properties">Destination list.</param>
    private static void ReadPropertiesMap(ref Utf8JsonReader reader, List<SchemaPropertyRow> properties)
    {
        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var name = reader.GetString()!;
            _ = reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            properties.Add(ReadPropertyDescriptor(ref reader, name));
        }
    }

    /// <summary>Reads one property schema descriptor.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="name">Property name.</param>
    /// <returns>Parsed property row.</returns>
    private static SchemaPropertyRow ReadPropertyDescriptor(ref Utf8JsonReader reader, string name)
    {
        byte[] propType = [];
        byte[] description = [];
        byte[] defaultVal = [];
        byte[] enumVals = [];

        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var key = reader.GetString()!;
            _ = reader.Read();
            if (key is "type" or "$ref")
            {
                propType = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (key == "description")
            {
                description = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (key == "default")
            {
                defaultVal = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else if (key == "enum")
            {
                enumVals = SchemaSourceHelper.ReadJsonValueSummary(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }

        return new(name, propType, defaultVal, enumVals, description);
    }

    /// <summary>Reads <c>$defs</c> / <c>definitions</c> sub-schemas.</summary>
    /// <param name="reader">Reader at <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="defs">Destination dictionary.</param>
    private static void ReadDefinitionsMap(ref Utf8JsonReader reader, Dictionary<string, SchemaObjectModel> defs)
    {
        while (SchemaSourceHelper.ReadNextProperty(ref reader))
        {
            var defName = reader.GetString()!;
            _ = reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            defs[defName] = ReadSchemaObject(ref reader, null);
        }
    }

    /// <summary>Writes the description and property table for <paramref name="schema"/>.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="schema">Parsed schema object.</param>
    private static void WriteSchemaBody(IBufferWriter<byte> writer, in SchemaObjectModel schema)
    {
        if (schema.Description.Length > 0)
        {
            writer.Write(schema.Description);
            writer.Write("\n\n"u8);
        }

        if (schema.SchemaType.Length > 0)
        {
            writer.Write("**Type:** `"u8);
            writer.Write(schema.SchemaType);
            writer.Write("`\n\n"u8);
        }

        if (schema.Properties.Length == 0)
        {
            return;
        }

        writer.Write("| Property | Type | Required | Default | Description |\n|---|---|---|---|---|\n"u8);
        for (var i = 0; i < schema.Properties.Length; i++)
        {
            WritePropertyRow(writer, schema.Properties[i], schema.Required.Contains(schema.Properties[i].Name));
        }

        writer.Write("\n"u8);
    }

    /// <summary>Writes one Markdown table row for <paramref name="prop"/>.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="prop">Property descriptor.</param>
    /// <param name="isRequired">Whether the property is required.</param>
    private static void WritePropertyRow(IBufferWriter<byte> writer, in SchemaPropertyRow prop, bool isRequired)
    {
        writer.Write("| `"u8);
        writer.Write(Encoding.UTF8.GetBytes(prop.Name));
        writer.Write("` | `"u8);
        writer.Write(prop.PropType.Length > 0 ? prop.PropType : "any"u8);
        writer.Write("` | "u8);
        writer.Write(isRequired ? "**Yes**"u8 : "No"u8);
        writer.Write(" | "u8);
        if (prop.DefaultValue.Length > 0)
        {
            writer.Write("`"u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, prop.DefaultValue);
            writer.Write("`"u8);
        }
        else
        {
            writer.Write("-"u8);
        }

        writer.Write(" | "u8);
        SchemaSourceHelper.WriteEscapedTableCell(writer, prop.Description);
        if (prop.EnumValues.Length > 0)
        {
            if (prop.Description.Length > 0)
            {
                writer.Write(" "u8);
            }

            writer.Write("Allowed: `"u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, prop.EnumValues);
            writer.Write("`"u8);
        }

        writer.Write(" |\n"u8);
    }

    /// <summary>Captured JSON Schema object metadata.</summary>
    /// <param name="Title">Title bytes.</param>
    /// <param name="Description">Description bytes.</param>
    /// <param name="SchemaType">Type or ref bytes.</param>
    /// <param name="Required">Required property names.</param>
    /// <param name="Properties">Object properties.</param>
    private readonly record struct SchemaObjectModel(
        byte[] Title,
        byte[] Description,
        byte[] SchemaType,
        HashSet<string> Required,
        SchemaPropertyRow[] Properties);

    /// <summary>Captured JSON Schema property row.</summary>
    /// <param name="Name">Property name.</param>
    /// <param name="PropType">Type or ref bytes.</param>
    /// <param name="DefaultValue">Default value bytes.</param>
    /// <param name="EnumValues">Comma-joined enum values.</param>
    /// <param name="Description">Description bytes.</param>
    private readonly record struct SchemaPropertyRow(
        string Name,
        byte[] PropType,
        byte[] DefaultValue,
        byte[] EnumValues,
        byte[] Description);
}
