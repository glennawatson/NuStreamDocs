// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Playgrounds;

/// <summary>Emits lazy-loaded sandboxed <c>&lt;iframe&gt;</c> code playgrounds (<c>dotnetfiddle</c>, <c>stackblitz</c>, <c>codepen</c>, <c>jsfiddle</c>, or custom <c>src</c>).</summary>
internal static class CodeSandboxWidgetEmitter
{
    /// <summary>Renders a <c>playground</c> block into <paramref name="writer"/>.</summary>
    /// <param name="content">Decoded UTF-8 key-value specification.</param>
    /// <param name="writer">Destination buffer.</param>
    internal static void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
    {
        ParseKeyValues(content, out var provider, out var id, out var title, out var customSrc);
        var embedUrl = ResolveEmbedUrl(provider, id, customSrc);
        var resolvedTitle = title.Length > 0 ? title : [.. "Interactive Code Playground"u8];

        writer.Write("<figure class=\"nd-code-playground\"><iframe class=\"nd-code-playground-frame\" loading=\"lazy\" sandbox=\"allow-scripts allow-forms allow-same-origin\" src=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, embedUrl, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\" title=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, resolvedTitle, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\"></iframe><figcaption class=\"nd-code-playground-caption\">"u8);
        XmlEntityEscaper.WriteEscaped(writer, resolvedTitle, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("</figcaption></figure>"u8);
    }

    /// <summary>Parses <c>key=value</c> lines from <paramref name="content"/>.</summary>
    /// <param name="content">UTF-8 lines.</param>
    /// <param name="provider">Provider identifier.</param>
    /// <param name="id">Snippet/fiddle identifier.</param>
    /// <param name="title">Accessible frame title.</param>
    /// <param name="customSrc">Explicit embed URL override.</param>
    private static void ParseKeyValues(
        ReadOnlySpan<byte> content,
        out string provider,
        out string id,
        out byte[] title,
        out byte[] customSrc)
    {
        provider = "dotnetfiddle";
        id = string.Empty;
        title = [];
        customSrc = [];

        var cursor = 0;
        while (cursor < content.Length)
        {
            var nl = content[cursor..].IndexOf((byte)'\n');
            ReadOnlySpan<byte> line;
            if (nl < 0)
            {
                line = content[cursor..].Trim(" \t\r"u8);
                cursor = content.Length;
            }
            else
            {
                line = content.Slice(cursor, nl).Trim(" \t\r"u8);
                cursor += nl + 1;
            }

            var eq = line.IndexOf((byte)'=');
            if (eq <= 0)
            {
                continue;
            }

            AssignKeyValue(line[..eq].Trim(" \t"u8), line[(eq + 1)..].Trim(" \t"u8), ref provider, ref id, ref title, ref customSrc);
        }
    }

    /// <summary>Assigns a single <c>key=value</c> pair into the target fields.</summary>
    /// <param name="key">Trimmed key bytes.</param>
    /// <param name="val">Trimmed value bytes.</param>
    /// <param name="provider">Provider slot.</param>
    /// <param name="id">Identifier slot.</param>
    /// <param name="title">Title slot.</param>
    /// <param name="customSrc">Explicit URL slot.</param>
    private static void AssignKeyValue(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> val,
        ref string provider,
        ref string id,
        ref byte[] title,
        ref byte[] customSrc)
    {
        if (key.SequenceEqual("provider"u8))
        {
            provider = Encoding.UTF8.GetString(val).ToLowerInvariant();
        }
        else if (key.SequenceEqual("id"u8))
        {
            id = Encoding.UTF8.GetString(val);
        }
        else if (key.SequenceEqual("title"u8))
        {
            title = val.ToArray();
        }
        else if (key.SequenceEqual("src"u8))
        {
            customSrc = val.ToArray();
        }
    }

    /// <summary>Resolves the embed URL from <paramref name="provider"/>, <paramref name="id"/>, or <paramref name="customSrc"/>.</summary>
    /// <param name="provider">Lowercase provider key.</param>
    /// <param name="id">Snippet identifier.</param>
    /// <param name="customSrc">Explicit URL override.</param>
    /// <returns>UTF-8 embed URL bytes.</returns>
    private static byte[] ResolveEmbedUrl(string provider, string id, byte[] customSrc)
    {
        if (customSrc.Length > 0)
        {
            return customSrc;
        }

        var url = provider switch
        {
            "stackblitz" => $"https://stackblitz.com/edit/{id}?embed=1",
            "codepen" => $"https://codepen.io/embed/{id}",
            "jsfiddle" => $"https://jsfiddle.net/{id}/embedded/",
            _ => $"https://dotnetfiddle.net/Widget/{id}",
        };
        return Encoding.UTF8.GetBytes(url);
    }
}
