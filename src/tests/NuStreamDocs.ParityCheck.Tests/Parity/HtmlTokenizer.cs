// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Frozen;
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Splits HTML into tags, comments and text, canonicalizing tags as it goes.</summary>
internal static class HtmlTokenizer
{
    /// <summary>Start of an HTML comment.</summary>
    private const string CommentOpen = "<!--";

    /// <summary>End of an HTML comment.</summary>
    private const string CommentClose = "-->";

    /// <summary>Tags that never have content, so every spelling of their end is equivalent.</summary>
    private static readonly FrozenSet<string> VoidElements = FrozenSet.ToFrozenSet(
        ["br", "hr", "img", "input", "meta", "link", "col", "area", "base", "wbr", "source"],
        StringComparer.Ordinal);

    /// <summary>Splits HTML into tokens.</summary>
    /// <param name="html">HTML to split.</param>
    /// <returns>The tokens in document order.</returns>
    internal static List<HtmlToken> Tokenize(string html)
    {
        var tokens = new List<HtmlToken>();
        var text = new StringBuilder();
        var i = 0;
        while (i < html.Length)
        {
            if (html[i] == '<' && TryReadTag(html, i, out var tag, out var next))
            {
                FlushText(tokens, text);
                tokens.Add(tag!);
                i = next;
                continue;
            }

            _ = text.Append(html[i]);
            i++;
        }

        FlushText(tokens, text);
        return tokens;
    }

    /// <summary>Adds the pending text as a token and clears it.</summary>
    /// <param name="tokens">Token list.</param>
    /// <param name="text">Pending text.</param>
    private static void FlushText(List<HtmlToken> tokens, StringBuilder text)
    {
        if (text.Length is 0)
        {
            return;
        }

        var raw = text.ToString();
        tokens.Add(HtmlToken.ForText(HtmlText.Canonicalize(raw, false), raw));
        _ = text.Clear();
    }

    /// <summary>Reads a tag or comment starting at <paramref name="start"/>.</summary>
    /// <param name="html">The document.</param>
    /// <param name="start">Index of the <c>&lt;</c>.</param>
    /// <param name="token">The token when the method returns <see langword="true"/>.</param>
    /// <param name="next">Index after the token.</param>
    /// <returns><see langword="true"/> when a tag or comment starts here.</returns>
    private static bool TryReadTag(string html, int start, out HtmlToken? token, out int next)
    {
        token = null;
        next = start;
        if (string.CompareOrdinal(html, start, CommentOpen, 0, CommentOpen.Length) == 0)
        {
            return TryReadComment(html, start, out token, out next);
        }

        var i = start + 1;
        var closing = i < html.Length && html[i] == '/';
        if (closing)
        {
            i++;
        }

        if (!TryReadName(html, ref i, out var name) || !TryReadAttributes(html, ref i, out var attributes, out var selfClosing))
        {
            return false;
        }

        token = BuildTag(html[start..i], name, closing, selfClosing, attributes);
        next = i;
        return true;
    }

    /// <summary>Reads a comment starting at <paramref name="start"/>.</summary>
    /// <param name="html">The document.</param>
    /// <param name="start">Index of the <c>&lt;</c>.</param>
    /// <param name="token">The comment token when the method returns <see langword="true"/>.</param>
    /// <param name="next">Index after the comment.</param>
    /// <returns><see langword="false"/> when the comment is unterminated.</returns>
    private static bool TryReadComment(string html, int start, out HtmlToken? token, out int next)
    {
        token = null;
        next = start;
        var close = html.IndexOf(CommentClose, start + CommentOpen.Length, StringComparison.Ordinal);
        if (close < 0)
        {
            return false;
        }

        next = close + CommentClose.Length;
        token = HtmlToken.ForComment(html[start..next]);
        return true;
    }

    /// <summary>Reads a tag name at <paramref name="i"/>.</summary>
    /// <param name="html">The document.</param>
    /// <param name="i">Index of the first name character; advanced past the name.</param>
    /// <param name="name">Lower-case tag name.</param>
    /// <returns><see langword="false"/> when no name starts here.</returns>
    private static bool TryReadName(string html, ref int i, out string name)
    {
        name = string.Empty;
        var nameStart = i;
        if (i >= html.Length || !char.IsAsciiLetter(html[i]))
        {
            return false;
        }

        while (i < html.Length && (char.IsAsciiLetterOrDigit(html[i]) || html[i] is ':' or '-' or '_'))
        {
            i++;
        }

        name = html[nameStart..i].ToLowerInvariant();
        return true;
    }

    /// <summary>Reads attributes up to the end of the tag.</summary>
    /// <param name="html">The document.</param>
    /// <param name="i">Index after the tag name; advanced past the closing angle bracket.</param>
    /// <param name="attributes">The attributes in source order.</param>
    /// <param name="selfClosing">Whether the tag ends with <c>/&gt;</c>.</param>
    /// <returns><see langword="false"/> when the tag is unterminated or has an unterminated quoted value.</returns>
    private static bool TryReadAttributes(string html, ref int i, out List<HtmlAttribute> attributes, out bool selfClosing)
    {
        attributes = [];
        selfClosing = false;
        SkipWhitespace(html, ref i);
        while (i < html.Length)
        {
            var before = i;
            if (AtTagEnd(html, ref i, out selfClosing))
            {
                return true;
            }

            if (i == before)
            {
                if (!TryReadAttribute(html, ref i, out var attribute))
                {
                    return false;
                }

                attributes.Add(attribute);
            }

            SkipWhitespace(html, ref i);
        }

        return false;
    }

    /// <summary>Consumes the end of a tag when <paramref name="i"/> is at <c>&gt;</c> or <c>/&gt;</c>; a lone slash is consumed and ignored.</summary>
    /// <param name="html">The document.</param>
    /// <param name="i">Index of the current character; advanced past what was consumed.</param>
    /// <param name="selfClosing">Whether the tag ended with <c>/&gt;</c>.</param>
    /// <returns><see langword="true"/> when the tag ended.</returns>
    private static bool AtTagEnd(string html, ref int i, out bool selfClosing)
    {
        selfClosing = false;
        if (html[i] == '>')
        {
            i++;
            return true;
        }

        if (html[i] != '/')
        {
            return false;
        }

        i++;
        selfClosing = i < html.Length && html[i] == '>';
        i += selfClosing ? 1 : 0;
        return selfClosing;
    }

    /// <summary>Builds the canonical token for a tag.</summary>
    /// <param name="raw">The tag as written.</param>
    /// <param name="name">Lower-case tag name.</param>
    /// <param name="closing">Whether the tag is a closing tag.</param>
    /// <param name="selfClosing">Whether the tag ends with <c>/&gt;</c>.</param>
    /// <param name="attributes">The attributes in source order.</param>
    /// <returns>The tag token.</returns>
    private static HtmlToken BuildTag(string raw, string name, bool closing, bool selfClosing, List<HtmlAttribute> attributes)
    {
        var kept = KeptAttributes(attributes, name is ['h', >= '1' and <= '6']);
        var builder = new StringBuilder("<");
        if (closing)
        {
            _ = builder.Append('/');
        }

        _ = builder.Append(name);
        AppendAttributes(builder, kept);
        _ = builder.Append(VoidElements.Contains(name) || selfClosing ? " />" : ">");
        return new(true, builder.ToString(), name, closing, ValueOf(attributes, "class"), kept.Count > 0, ValueOf(attributes, "id"), raw);
    }

    /// <summary>Appends attributes as <c> name="value"</c> pairs, or as bare names when they have no value.</summary>
    /// <param name="builder">Destination.</param>
    /// <param name="attributes">Attributes to append.</param>
    private static void AppendAttributes(StringBuilder builder, List<HtmlAttribute> attributes)
    {
        for (var i = 0; i < attributes.Count; i++)
        {
            _ = builder.Append(' ').Append(attributes[i].Name);
            if (attributes[i].Value is { } value)
            {
                _ = builder.Append("=\"").Append(value).Append('"');
            }
        }
    }

    /// <summary>Returns the attributes that survive normalization, ordered by name; class and style are dropped, and so is the id of a heading.</summary>
    /// <param name="attributes">The attributes in source order.</param>
    /// <param name="heading">Whether the tag is a heading.</param>
    /// <returns>The kept attributes; attributes with equal names keep their source order.</returns>
    private static List<HtmlAttribute> KeptAttributes(List<HtmlAttribute> attributes, bool heading)
    {
        var kept = new List<HtmlAttribute>(attributes.Count);
        for (var i = 0; i < attributes.Count; i++)
        {
            var attribute = attributes[i];
            if (attribute.Name is "class" or "style" || (heading && attribute.Name == "id"))
            {
                continue;
            }

            var index = kept.Count;
            while (index > 0 && string.CompareOrdinal(kept[index - 1].Name, attribute.Name) > 0)
            {
                index--;
            }

            kept.Insert(index, attribute);
        }

        return kept;
    }

    /// <summary>Returns the value of the first attribute with the given name.</summary>
    /// <param name="attributes">The attributes in source order.</param>
    /// <param name="name">Attribute name.</param>
    /// <returns>The value, or an empty string when the attribute is missing or has no value.</returns>
    private static string ValueOf(List<HtmlAttribute> attributes, string name)
    {
        for (var i = 0; i < attributes.Count; i++)
        {
            if (attributes[i].Name == name)
            {
                return attributes[i].Value ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>Advances <paramref name="i"/> past whitespace.</summary>
    /// <param name="html">The document.</param>
    /// <param name="i">Index to advance.</param>
    private static void SkipWhitespace(string html, ref int i)
    {
        while (i < html.Length && char.IsWhiteSpace(html[i]))
        {
            i++;
        }
    }

    /// <summary>Reads one attribute at <paramref name="i"/>.</summary>
    /// <param name="html">The document.</param>
    /// <param name="i">Index of the attribute name; advanced past the attribute.</param>
    /// <param name="attribute">The attribute when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when a quoted value is unterminated.</returns>
    private static bool TryReadAttribute(string html, ref int i, out HtmlAttribute attribute)
    {
        attribute = new(string.Empty, null);
        var nameStart = i;
        while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] is not ('=' or '>' or '/'))
        {
            i++;
        }

        var name = html[nameStart..i].ToLowerInvariant();
        SkipWhitespace(html, ref i);
        if (i >= html.Length || html[i] != '=')
        {
            attribute = new(name, null);
            return true;
        }

        i++;
        SkipWhitespace(html, ref i);
        if (!TryReadValue(html, ref i, out var value))
        {
            return false;
        }

        attribute = new(name, HtmlText.Canonicalize(value, true));
        return true;
    }

    /// <summary>Reads an attribute value, quoted or bare, at <paramref name="i"/>.</summary>
    /// <param name="html">The document.</param>
    /// <param name="i">Index of the value; advanced past it.</param>
    /// <param name="value">The value as written, without quotes.</param>
    /// <returns><see langword="false"/> when a quoted value is unterminated.</returns>
    private static bool TryReadValue(string html, ref int i, out string value)
    {
        value = string.Empty;
        if (i < html.Length && html[i] is '"' or '\'')
        {
            var quote = html[i];
            i++;
            var end = html.IndexOf(quote, i);
            if (end < 0)
            {
                return false;
            }

            value = html[i..end];
            i = end + 1;
            return true;
        }

        var valueStart = i;
        while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] != '>')
        {
            i++;
        }

        value = html[valueStart..i];
        return true;
    }
}
