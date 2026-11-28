// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Schemas;

/// <summary>Parses GraphQL Schema Definition Language (<c>.graphql</c> / <c>.gql</c>) in UTF-8 and emits a structured Markdown reference page.</summary>
internal static class GraphQlSdlPageBuilder
{
    /// <summary>Length of a <c>"""</c> GraphQL block-string delimiter.</summary>
    private const int TripleQuoteLength = 3;

    /// <summary>Initial output capacity for generated Markdown.</summary>
    private const int InitialBufferCapacity = 1024;

    /// <summary>ASCII whitespace and comma separators ignored between GraphQL tokens.</summary>
    private static readonly SearchValues<byte> IgnoredSeparators = SearchValues.Create(" \t\r\n,"u8);

    /// <summary>ASCII identifier characters valid in GraphQL names.</summary>
    private static readonly SearchValues<byte> IdentifierBytes =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"u8);

    /// <summary>Builds a synthetic Markdown page from UTF-8 GraphQL SDL bytes.</summary>
    /// <param name="sdlBytes">UTF-8 GraphQL SDL source.</param>
    /// <param name="title">Page title.</param>
    /// <param name="routePrefix">Target subdirectory prefix.</param>
    /// <returns>Single-element page array, or empty when no definitions exist.</returns>
    internal static SyntheticPage[] Build(ReadOnlySpan<byte> sdlBytes, string title, PathSegment routePrefix)
    {
        var definitions = ParseDefinitions(sdlBytes);
        if (definitions.Count == 0)
        {
            return [];
        }

        var titleBytes = Encoding.UTF8.GetBytes(title);
        var targetPath = SchemaSourceHelper.BuildTargetPath(routePrefix, title);
        var writer = new ArrayBufferWriter<byte>(InitialBufferCapacity);
        SchemaSourceHelper.WriteFrontMatterTitle(writer, titleBytes);

        for (var i = 0; i < definitions.Count; i++)
        {
            WriteDefinitionSection(writer, definitions[i]);
        }

        return [new(targetPath, writer.WrittenSpan.ToArray())];
    }

    /// <summary>Parses top-level <c>type</c>, <c>input</c>, <c>interface</c>, and <c>enum</c> blocks from <paramref name="sdl"/>.</summary>
    /// <param name="sdl">UTF-8 SDL bytes.</param>
    /// <returns>Parsed definitions in declaration order.</returns>
    private static List<SdlTypeDefinition> ParseDefinitions(ReadOnlySpan<byte> sdl)
    {
        List<SdlTypeDefinition> list = [];
        var cursor = 0;
        byte[] pendingDescription = [];

        while (cursor < sdl.Length)
        {
            SkipTrivia(sdl, ref cursor);
            if (cursor >= sdl.Length)
            {
                break;
            }

            if (sdl[cursor] == (byte)'"')
            {
                pendingDescription = ReadDescriptionLiteral(sdl, ref cursor);
                continue;
            }

            var keyword = ReadIdentifier(sdl, ref cursor);
            if (keyword.IsEmpty)
            {
                cursor++;
                pendingDescription = [];
                continue;
            }

            if (IsBlockKind(keyword, out var isEnum))
            {
                SkipTrivia(sdl, ref cursor);
                var typeName = ReadIdentifier(sdl, ref cursor);
                var entries = ReadBracedMembers(sdl, ref cursor, isEnum);
                list.Add(new(keyword.ToArray(), typeName.ToArray(), pendingDescription, entries, isEnum));
            }
            else
            {
                SkipPastNextBraceOrLine(sdl, ref cursor);
            }

            pendingDescription = [];
        }

        return list;
    }

    /// <summary>Checks whether <paramref name="keyword"/> introduces a documented GraphQL block.</summary>
    /// <param name="keyword">Identifier bytes.</param>
    /// <param name="isEnum"><c>true</c> when <paramref name="keyword"/> is <c>enum</c>.</param>
    /// <returns><c>true</c> for <c>type</c>, <c>input</c>, <c>interface</c>, or <c>enum</c>.</returns>
    private static bool IsBlockKind(ReadOnlySpan<byte> keyword, out bool isEnum)
    {
        if (keyword.SequenceEqual("enum"u8))
        {
            isEnum = true;
            return true;
        }

        isEnum = false;
        return keyword.SequenceEqual("type"u8)
            || keyword.SequenceEqual("input"u8)
            || keyword.SequenceEqual("interface"u8);
    }

    /// <summary>Reads members inside a <c>{ ... }</c> GraphQL type or enum body.</summary>
    /// <param name="sdl">SDL bytes.</param>
    /// <param name="cursor">Current byte offset.</param>
    /// <param name="isEnum"><c>true</c> for enum values; <c>false</c> for typed fields.</param>
    /// <returns>Parsed member rows.</returns>
    private static SdlMemberRow[] ReadBracedMembers(ReadOnlySpan<byte> sdl, ref int cursor, bool isEnum)
    {
        var openRel = sdl[cursor..].IndexOf((byte)'{');
        if (openRel < 0)
        {
            cursor = sdl.Length;
            return [];
        }

        cursor += openRel + 1;
        List<SdlMemberRow> rows = [];
        byte[] memberDesc = [];

        while (cursor < sdl.Length)
        {
            SkipTrivia(sdl, ref cursor);
            if (cursor >= sdl.Length || sdl[cursor] == (byte)'}')
            {
                cursor++;
                break;
            }

            if (sdl[cursor] == (byte)'"')
            {
                memberDesc = ReadDescriptionLiteral(sdl, ref cursor);
                continue;
            }

            rows.Add(ReadSingleMember(sdl, ref cursor, isEnum, memberDesc));
            memberDesc = [];
        }

        return [.. rows];
    }

    /// <summary>Reads a single field signature or enum value up to the end of its declaration.</summary>
    /// <param name="sdl">SDL bytes.</param>
    /// <param name="cursor">Current byte offset.</param>
    /// <param name="isEnum">Whether reading an enum member.</param>
    /// <param name="description">Preceding description literal.</param>
    /// <returns>Parsed member row.</returns>
    private static SdlMemberRow ReadSingleMember(ReadOnlySpan<byte> sdl, ref int cursor, bool isEnum, byte[] description)
    {
        var start = cursor;
        AdvanceToMemberEnd(sdl, ref cursor);

        var rawMember = sdl[start..cursor].Trim(" \t\r"u8);
        if (isEnum)
        {
            var space = rawMember.IndexOfAny((byte)' ', (byte)'@');
            var enumVal = space < 0 ? rawMember : rawMember[..space];
            return new(enumVal.ToArray(), [], description);
        }

        var colon = FindTopLevelColon(rawMember);
        if (colon < 0)
        {
            return new(rawMember.ToArray(), [], description);
        }

        var signature = rawMember[..colon].Trim(" \t"u8);
        var typePart = StripDirectives(rawMember[(colon + 1)..].Trim(" \t"u8));
        return new(signature.ToArray(), typePart.ToArray(), description);
    }

    /// <summary>Advances <paramref name="cursor"/> to the line break, comment, or closing brace ending a member.</summary>
    /// <param name="sdl">SDL bytes.</param>
    /// <param name="cursor">Current byte offset.</param>
    private static void AdvanceToMemberEnd(ReadOnlySpan<byte> sdl, ref int cursor)
    {
        var parenDepth = 0;
        while (cursor < sdl.Length)
        {
            var b = sdl[cursor];
            if (b == (byte)'(')
            {
                parenDepth++;
            }
            else if (b == (byte)')' && parenDepth > 0)
            {
                parenDepth--;
            }
            else if (parenDepth == 0 && b is (byte)'\n' or (byte)'}' or (byte)'#')
            {
                break;
            }

            cursor++;
        }
    }

    /// <summary>Finds the colon separating a GraphQL field (plus optional arguments) from its return type.</summary>
    /// <param name="member">Raw field declaration bytes.</param>
    /// <returns>Offset of the top-level <c>:</c>, or <c>-1</c>.</returns>
    private static int FindTopLevelColon(ReadOnlySpan<byte> member)
    {
        var depth = 0;
        for (var i = 0; i < member.Length; i++)
        {
            var b = member[i];
            if (b == (byte)'(')
            {
                depth++;
            }
            else if (b == (byte)')' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0 && b == (byte)':')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Strips trailing <c>@directive</c> annotations from a GraphQL field type expression.</summary>
    /// <param name="typePart">Trimmed return-type slice.</param>
    /// <returns>Clean type expression.</returns>
    private static ReadOnlySpan<byte> StripDirectives(ReadOnlySpan<byte> typePart)
    {
        var atIdx = typePart.IndexOf((byte)'@');
        return atIdx < 0 ? typePart : typePart[..atIdx].TrimEnd(" \t"u8);
    }

    /// <summary>Reads a single-quoted <c>"..."</c> or triple-quoted <c>"""..."""</c> GraphQL description string.</summary>
    /// <param name="sdl">SDL bytes.</param>
    /// <param name="cursor">Offset of the opening <c>"</c>.</param>
    /// <returns>Trimmed description UTF-8 bytes.</returns>
    private static byte[] ReadDescriptionLiteral(ReadOnlySpan<byte> sdl, ref int cursor)
    {
        if (sdl[cursor..].StartsWith("\"\"\""u8))
        {
            cursor += TripleQuoteLength;
            var endRel = sdl[cursor..].IndexOf("\"\"\""u8);
            if (endRel < 0)
            {
                var tail = sdl[cursor..].Trim(" \t\r\n"u8);
                cursor = sdl.Length;
                return tail.ToArray();
            }

            var content = sdl.Slice(cursor, endRel).Trim(" \t\r\n"u8);
            cursor += endRel + TripleQuoteLength;
            return content.ToArray();
        }

        cursor++;
        var closeRel = sdl[cursor..].IndexOf((byte)'"');
        if (closeRel < 0)
        {
            var rest = sdl[cursor..].Trim(" \t\r\n"u8);
            cursor = sdl.Length;
            return rest.ToArray();
        }

        var single = sdl.Slice(cursor, closeRel).Trim(" \t"u8);
        cursor += closeRel + 1;
        return single.ToArray();
    }

    /// <summary>Skips whitespace, commas, and <c>#</c> line comments.</summary>
    /// <param name="sdl">SDL bytes.</param>
    /// <param name="cursor">Current offset.</param>
    private static void SkipTrivia(ReadOnlySpan<byte> sdl, ref int cursor)
    {
        while (cursor < sdl.Length)
        {
            if (IgnoredSeparators.Contains(sdl[cursor]))
            {
                cursor++;
                continue;
            }

            if (sdl[cursor] != (byte)'#')
            {
                return;
            }

            var nl = sdl[cursor..].IndexOf((byte)'\n');
            cursor = nl < 0 ? sdl.Length : cursor + nl + 1;
        }
    }

    /// <summary>Reads an ASCII identifier token at <paramref name="cursor"/>.</summary>
    /// <param name="sdl">SDL bytes.</param>
    /// <param name="cursor">Current offset.</param>
    /// <returns>Identifier slice.</returns>
    private static ReadOnlySpan<byte> ReadIdentifier(ReadOnlySpan<byte> sdl, ref int cursor)
    {
        var start = cursor;
        while (cursor < sdl.Length && IdentifierBytes.Contains(sdl[cursor]))
        {
            cursor++;
        }

        return sdl[start..cursor];
    }

    /// <summary>Skips an unrecognized top-level construct (such as <c>scalar</c> or <c>schema</c>).</summary>
    /// <param name="sdl">SDL bytes.</param>
    /// <param name="cursor">Current offset.</param>
    private static void SkipPastNextBraceOrLine(ReadOnlySpan<byte> sdl, ref int cursor)
    {
        while (cursor < sdl.Length && sdl[cursor] is not ((byte)'\n' or (byte)'{'))
        {
            cursor++;
        }

        if (cursor >= sdl.Length || sdl[cursor] != (byte)'{')
        {
            return;
        }

        var closeRel = sdl[cursor..].IndexOf((byte)'}');
        cursor = closeRel < 0 ? sdl.Length : cursor + closeRel + 1;
    }

    /// <summary>Emits the Markdown section for one GraphQL type or enum.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="def">Parsed type definition.</param>
    private static void WriteDefinitionSection(IBufferWriter<byte> writer, in SdlTypeDefinition def)
    {
        writer.Write("## `"u8);
        writer.Write(def.TypeName);
        writer.Write("` ("u8);
        writer.Write(def.Kind);
        writer.Write(")\n\n"u8);

        if (def.Description.Length > 0)
        {
            writer.Write(def.Description);
            writer.Write("\n\n"u8);
        }

        if (def.Members.Length == 0)
        {
            return;
        }

        if (def.IsEnum)
        {
            writer.Write("| Value | Description |\n|---|---|\n"u8);
            for (var i = 0; i < def.Members.Length; i++)
            {
                writer.Write("| `"u8);
                SchemaSourceHelper.WriteEscapedTableCell(writer, def.Members[i].Signature);
                writer.Write("` | "u8);
                SchemaSourceHelper.WriteEscapedTableCell(writer, def.Members[i].Description);
                writer.Write(" |\n"u8);
            }

            writer.Write("\n"u8);
            return;
        }

        writer.Write("| Field | Type | Description |\n|---|---|---|\n"u8);
        for (var i = 0; i < def.Members.Length; i++)
        {
            writer.Write("| `"u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, def.Members[i].Signature);
            writer.Write("` | `"u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, def.Members[i].ReturnType);
            writer.Write("` | "u8);
            SchemaSourceHelper.WriteEscapedTableCell(writer, def.Members[i].Description);
            writer.Write(" |\n"u8);
        }

        writer.Write("\n"u8);
    }

    /// <summary>Captured GraphQL SDL type or enum block.</summary>
    /// <param name="Kind">Keyword (<c>type</c>, <c>input</c>, <c>interface</c>, or <c>enum</c>).</param>
    /// <param name="TypeName">Identifier name.</param>
    /// <param name="Description">Optional description.</param>
    /// <param name="Members">Parsed fields or enum values.</param>
    /// <param name="IsEnum">Whether this block is an enum.</param>
    private readonly record struct SdlTypeDefinition(
        byte[] Kind,
        byte[] TypeName,
        byte[] Description,
        SdlMemberRow[] Members,
        bool IsEnum);

    /// <summary>Captured GraphQL field or enum value.</summary>
    /// <param name="Signature">Field name (plus arguments) or enum symbol.</param>
    /// <param name="ReturnType">Field return type (empty for enums).</param>
    /// <param name="Description">Optional description.</param>
    private readonly record struct SdlMemberRow(
        byte[] Signature,
        byte[] ReturnType,
        byte[] Description);
}
