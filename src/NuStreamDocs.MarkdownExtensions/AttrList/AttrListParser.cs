// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.MarkdownExtensions.AttrList;

/// <summary>
/// Parses the body of a <c>{: ... }</c> attr-list token into an
/// <c>id</c>, a list of class names, and an arbitrary key/value map.
/// </summary>
internal static class AttrListParser
{
    /// <summary>Parses <paramref name="text"/> into <c>(id, classes, kv)</c>.</summary>
    /// <param name="text">Token body, without the surrounding <c>{:</c> and <c>}</c>.</param>
    /// <returns>The parsed parts.</returns>
    public static (string Id, List<string> Classes, List<KeyValuePair<string, string>> KeyValues) Parse(string text)
    {
        var id = string.Empty;
        var classes = new List<string>();
        var kv = new List<KeyValuePair<string, string>>();

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case '#':
                    {
                        i = ReadToken(text, i + 1, out id);
                        continue;
                    }

                case '.':
                    {
                        i = ReadToken(text, i + 1, out var cls);
                        if (cls.Length > 0)
                        {
                            classes.Add(cls);
                        }

                        continue;
                    }

                default:
                    {
                        i = ReadKeyValue(text, i, kv);
                        break;
                    }
            }
        }

        return (id, classes, kv);
    }

    /// <summary>Reads a non-space identifier-like token starting at <paramref name="offset"/>.</summary>
    /// <param name="text">Source text.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="token">Set to the consumed token.</param>
    /// <returns>Offset just past the token.</returns>
    private static int ReadToken(string text, int offset, out string token)
    {
        var start = offset;
        while (offset < text.Length && !char.IsWhiteSpace(text[offset]))
        {
            offset++;
        }

        token = text[start..offset];
        return offset;
    }

    /// <summary>Reads a <c>key="value"</c> or <c>key=value</c> pair starting at <paramref name="offset"/>.</summary>
    /// <param name="text">Source text.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="kv">Output key/value list.</param>
    /// <returns>Offset just past the pair.</returns>
    private static int ReadKeyValue(string text, int offset, List<KeyValuePair<string, string>> kv)
    {
        var keyStart = offset;
        while (offset < text.Length && text[offset] is not ('=' or ' ' or '\t'))
        {
            offset++;
        }

        var key = text[keyStart..offset];
        if (offset >= text.Length || text[offset] != '=')
        {
            // Bare key (no value) — treat as a flag attribute.
            if (key.Length > 0)
            {
                kv.Add(new(key, string.Empty));
            }

            return offset;
        }

        offset++;
        return ReadValue(text, offset, key, kv);
    }

    /// <summary>Reads the value half of a key/value pair, handling optional quoting.</summary>
    /// <param name="text">Source text.</param>
    /// <param name="offset">Start offset (just past the <c>=</c>).</param>
    /// <param name="key">Key string already parsed.</param>
    /// <param name="kv">Output key/value list.</param>
    /// <returns>Offset just past the value.</returns>
    private static int ReadValue(string text, int offset, string key, List<KeyValuePair<string, string>> kv)
    {
        if (offset >= text.Length)
        {
            kv.Add(new(key, string.Empty));
            return offset;
        }

        var quote = text[offset];
        if (quote is '"' or '\'')
        {
            offset++;
            var valueStart = offset;
            while (offset < text.Length && text[offset] != quote)
            {
                offset++;
            }

            kv.Add(new(key, text[valueStart..offset]));
            return offset < text.Length ? offset + 1 : offset;
        }

        var bareStart = offset;
        while (offset < text.Length && !char.IsWhiteSpace(text[offset]))
        {
            offset++;
        }

        kv.Add(new(key, text[bareStart..offset]));
        return offset;
    }
}
