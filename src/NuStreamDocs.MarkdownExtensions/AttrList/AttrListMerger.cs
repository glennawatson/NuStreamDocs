// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;

namespace NuStreamDocs.MarkdownExtensions.AttrList;

/// <summary>
/// Merges parsed attr-list parts into the existing attribute list of
/// an HTML opening tag.
/// </summary>
internal static partial class AttrListMerger
{
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

    /// <summary>Parses an existing attribute fragment into a name → value dictionary.</summary>
    /// <param name="attrs">Attribute fragment.</param>
    /// <returns>Ordered insertion-preserving dictionary.</returns>
    private static Dictionary<string, string> ParseExisting(string attrs)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(attrs))
        {
            return result;
        }

        foreach (Match match in ExistingAttrRegex().Matches(attrs))
        {
            var name = match.Groups["name"].Value;
            var quoted = match.Groups["quoted"];
            var bare = match.Groups["bare"];
            string value;
            if (quoted.Success)
            {
                value = quoted.Value;
            }
            else if (bare.Success)
            {
                value = bare.Value;
            }
            else
            {
                value = string.Empty;
            }

            result[name] = value;
        }

        return result;
    }

    /// <summary>Escapes <c>&amp;</c> and <c>"</c> for use inside a double-quoted HTML attribute.</summary>
    /// <param name="value">Raw attribute value.</param>
    /// <returns>Escaped value.</returns>
    private static string EscapeAttr(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>Matches one HTML attribute (<c>name</c>, <c>name=value</c>, or <c>name="value"</c>).</summary>
    /// <returns>Compiled regex.</returns>
    [GeneratedRegex("(?<name>[A-Za-z_:][A-Za-z0-9_.:-]*)(?:=(?:\"(?<quoted>[^\"]*)\"|(?<bare>[^\\s]+)))?", RegexOptions.Compiled)]
    private static partial Regex ExistingAttrRegex();
}
