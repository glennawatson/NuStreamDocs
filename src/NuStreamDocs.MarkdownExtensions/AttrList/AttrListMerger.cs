// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.MarkdownExtensions.AttrList;

/// <summary>
/// Merges parsed attr-list parts into the existing attribute list of
/// an HTML opening tag.
/// </summary>
internal static class AttrListMerger
{
    /// <summary>Allowed first-character set for an attribute name (ASCII letters, underscore, colon).</summary>
    private static readonly SearchValues<char> AttrNameStart = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_:");

    /// <summary>Allowed continuation set for an attribute name (letters, digits, underscore, colon, dot, dash).</summary>
    private static readonly SearchValues<char> AttrNameContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_:.-");

    /// <summary>Whitespace inside an HTML tag-attribute fragment.</summary>
    private static readonly SearchValues<char> AttrWhitespace = SearchValues.Create(" \t\r\n");

    /// <summary>Returns <paramref name="existingAttrs"/> with <paramref name="id"/>, <paramref name="classes"/>, and <paramref name="kv"/> merged in.</summary>
    /// <param name="existingAttrs">Attribute fragment as it appears between the tag name and the closing <c>&gt;</c>; may be empty or start with a space.</param>
    /// <param name="id">Attr-list id; empty when not specified.</param>
    /// <param name="classes">Attr-list class tokens (possibly empty).</param>
    /// <param name="kv">Attr-list key/value pairs (possibly empty).</param>
    /// <returns>The merged attribute fragment, including a leading space when non-empty.</returns>
    public static string Merge(string existingAttrs, string id, List<string> classes, List<KeyValuePair<string, string>> kv)
    {
        var attrs = ParseExisting(existingAttrs);

        if (!string.IsNullOrEmpty(id))
        {
            attrs["id"] = id;
        }

        if (classes.Count > 0)
        {
            attrs.TryGetValue("class", out var existingClass);
            attrs["class"] = string.IsNullOrEmpty(existingClass)
                ? string.Join(' ', classes)
                : $"{existingClass} {string.Join(' ', classes)}";
        }

        for (var i = 0; i < kv.Count; i++)
        {
            attrs[kv[i].Key] = kv[i].Value;
        }

        if (attrs.Count is 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var entry in attrs)
        {
            sb.Append(' ').Append(entry.Key);
            if (!string.IsNullOrEmpty(entry.Value))
            {
                sb.Append("=\"").Append(EscapeAttr(entry.Value)).Append('"');
            }
        }

        return sb.ToString();
    }

    /// <summary>Parses an existing attribute fragment into a name → value dictionary using a span scanner.</summary>
    /// <param name="attrs">Attribute fragment.</param>
    /// <returns>Insertion-order-preserving dictionary.</returns>
    private static Dictionary<string, string> ParseExisting(string attrs)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var span = attrs.AsSpan();
        var pos = 0;
        while (TryReadOneAttribute(span, ref pos, out var name, out var value))
        {
            result[name] = value;
        }

        return result;
    }

    /// <summary>
    /// Reads one HTML attribute (<c>name</c>, <c>name=value</c>, or
    /// <c>name="value"</c>) from <paramref name="span"/> at
    /// <paramref name="pos"/>, advancing <paramref name="pos"/> past it.
    /// </summary>
    /// <param name="span">Attribute fragment.</param>
    /// <param name="pos">Cursor; advanced past whitespace and the matched attribute on success.</param>
    /// <param name="name">Attribute name on success.</param>
    /// <param name="value">Attribute value (empty when no <c>=</c> follows the name).</param>
    /// <returns>True when an attribute was read; false at end-of-input.</returns>
    private static bool TryReadOneAttribute(ReadOnlySpan<char> span, ref int pos, out string name, out string value)
    {
        SkipWhitespace(span, ref pos);
        if (pos >= span.Length || !AttrNameStart.Contains(span[pos]))
        {
            // No name here — advance one char to guarantee forward progress and signal end-of-attributes.
            if (pos < span.Length)
            {
                pos++;
            }

            name = string.Empty;
            value = string.Empty;
            return pos < span.Length;
        }

        name = ReadName(span, ref pos);
        value = pos < span.Length && span[pos] is '=' ? ReadValue(span, ref pos) : string.Empty;
        return true;
    }

    /// <summary>Advances <paramref name="pos"/> past any leading attribute-whitespace.</summary>
    /// <param name="span">Attribute fragment.</param>
    /// <param name="pos">Cursor.</param>
    private static void SkipWhitespace(ReadOnlySpan<char> span, ref int pos)
    {
        while (pos < span.Length && AttrWhitespace.Contains(span[pos]))
        {
            pos++;
        }
    }

    /// <summary>Reads an attribute name starting at <paramref name="pos"/>; expects the cursor to already be on a valid name-start character.</summary>
    /// <param name="span">Attribute fragment.</param>
    /// <param name="pos">Cursor; advanced past the name.</param>
    /// <returns>The attribute name.</returns>
    private static string ReadName(ReadOnlySpan<char> span, ref int pos)
    {
        var start = pos;
        pos++;
        while (pos < span.Length && AttrNameContinue.Contains(span[pos]))
        {
            pos++;
        }

        return span[start..pos].ToString();
    }

    /// <summary>Reads an attribute value: <c>"..."</c> when the cursor is at <c>=</c>+<c>"</c>, otherwise an unquoted run up to whitespace.</summary>
    /// <param name="span">Attribute fragment.</param>
    /// <param name="pos">Cursor; expected to be on <c>=</c>; advanced past the value.</param>
    /// <returns>The attribute value.</returns>
    private static string ReadValue(ReadOnlySpan<char> span, ref int pos)
    {
        pos++; // skip '='
        if (pos < span.Length && span[pos] is '"')
        {
            return ReadQuotedValue(span, ref pos);
        }

        return ReadBareValue(span, ref pos);
    }

    /// <summary>Reads a double-quoted attribute value (no escape handling — matches the HTML attribute fragment shape).</summary>
    /// <param name="span">Attribute fragment.</param>
    /// <param name="pos">Cursor; expected to be on <c>"</c>; advanced past the closing quote.</param>
    /// <returns>The unquoted value text.</returns>
    private static string ReadQuotedValue(ReadOnlySpan<char> span, ref int pos)
    {
        pos++; // skip opening quote
        var start = pos;
        while (pos < span.Length && span[pos] is not '"')
        {
            pos++;
        }

        var value = span[start..pos].ToString();
        if (pos < span.Length)
        {
            pos++; // skip closing quote
        }

        return value;
    }

    /// <summary>Reads an unquoted attribute value — everything up to the next attribute-whitespace.</summary>
    /// <param name="span">Attribute fragment.</param>
    /// <param name="pos">Cursor; advanced past the run.</param>
    /// <returns>The unquoted value text.</returns>
    private static string ReadBareValue(ReadOnlySpan<char> span, ref int pos)
    {
        var start = pos;
        while (pos < span.Length && !AttrWhitespace.Contains(span[pos]))
        {
            pos++;
        }

        return span[start..pos].ToString();
    }

    /// <summary>Escapes <c>&amp;</c> and <c>"</c> for use inside a double-quoted HTML attribute.</summary>
    /// <param name="value">Raw attribute value.</param>
    /// <returns>Escaped value.</returns>
    private static string EscapeAttr(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
}
