// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>
/// Forward-only tag tokenizer over UTF-8 HTML bytes. Yields one start or end tag per
/// <see cref="MoveNext"/>; for rawtext elements (<c>script</c>, <c>style</c>, <c>title</c>,
/// <c>textarea</c>) it skips the inner content so stray <c>&lt;</c> bytes are not mistaken for
/// markup, and exposes that content through <see cref="RawText"/>.
/// </summary>
internal ref struct HtmlTagCursor
{
    /// <summary>Length of the comment opener <c>&lt;!--</c>.</summary>
    private const int CommentOpenLength = 4;

    /// <summary>Length of the comment closer <c>--&gt;</c>.</summary>
    private const int CommentCloseLength = 3;

    /// <summary>Length of an end-tag prefix <c>&lt;/</c>.</summary>
    private const int EndTagPrefixLength = 2;

    /// <summary>The HTML bytes being scanned.</summary>
    private readonly ReadOnlySpan<byte> _html;

    /// <summary>Offset of the next byte to examine.</summary>
    private int _pos;

    /// <summary>Initializes a new instance of the <see cref="HtmlTagCursor"/> struct.</summary>
    /// <param name="html">UTF-8 HTML bytes to scan.</param>
    public HtmlTagCursor(ReadOnlySpan<byte> html)
    {
        _html = html;
        _pos = 0;
    }

    /// <summary>Gets the current tag's name bytes (compare ASCII case-insensitively).</summary>
    public ReadOnlySpan<byte> Name { get; private set; }

    /// <summary>Gets the attribute text between the current tag's name and its closing bracket.</summary>
    public ReadOnlySpan<byte> Attributes { get; private set; }

    /// <summary>Gets the raw inner content of a rawtext element; empty for every other tag.</summary>
    public ReadOnlySpan<byte> RawText { get; private set; }

    /// <summary>Gets a value indicating whether the current token is an end tag (<c>&lt;/name&gt;</c>).</summary>
    public bool IsEndTag { get; private set; }

    /// <summary>Gets a value indicating whether the current token is self-closing (<c>&lt;name/&gt;</c>).</summary>
    public bool IsSelfClosing { get; private set; }

    /// <summary>Gets the byte offset of the <c>&lt;</c> that opens the current tag.</summary>
    public int TagStart { get; private set; }

    /// <summary>Gets the byte offset just past the <c>&gt;</c> that closes the current tag (or, for rawtext elements, past its end tag).</summary>
    public int TagEnd { get; private set; }

    /// <summary>Gets the value of <paramref name="name"/> on the current tag.</summary>
    /// <param name="name">Attribute name.</param>
    /// <param name="value">On success, the unquoted value bytes.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    public readonly bool TryGetAttribute(ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value) =>
        HtmlAttr.TryGet(Attributes, name, out value);

    /// <summary>Tests whether the current tag has <paramref name="name"/>.</summary>
    /// <param name="name">Attribute name.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    public readonly bool HasAttribute(ReadOnlySpan<byte> name) =>
        HtmlAttr.Has(Attributes, name);

    /// <summary>Advances to the next tag.</summary>
    /// <returns><see langword="true"/> when a tag was found; <see langword="false"/> at end of input.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Hand-rolled HTML5 tokenizer step; the branching tracks markup shapes (comments, end tags, self-closing, rawtext), not nested logic.")]
    [SuppressMessage(
        "Sonar Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "Hand-rolled HTML5 tokenizer step; the branching tracks markup shapes (comments, end tags, self-closing, rawtext), not nested logic.")]
    public bool MoveNext()
    {
        Reset();
        while (_pos < _html.Length)
        {
            var rel = _html[_pos..].IndexOf((byte)'<');
            if (rel < 0)
            {
                _pos = _html.Length;
                return false;
            }

            var lt = _pos + rel;
            if (StartsWithAt(lt, "<!--"u8))
            {
                if (!SkipComment(lt))
                {
                    return false;
                }

                continue;
            }

            var cursor = lt + 1;
            var isEnd = cursor < _html.Length && _html[cursor] == (byte)'/';
            if (isEnd)
            {
                cursor++;
            }

            if (cursor >= _html.Length || !AsciiByteHelpers.IsAsciiLetter(_html[cursor]))
            {
                _pos = lt + 1;
                continue;
            }

            return ReadTag(lt, cursor, isEnd);
        }

        return false;
    }

    /// <summary>True for bytes allowed inside a tag name (slug bytes plus <c>:</c> for namespaced names).</summary>
    /// <param name="b">Byte to test.</param>
    /// <returns><see langword="true"/> when the byte continues a tag name.</returns>
    private static bool IsNameChar(byte b) =>
        AsciiByteHelpers.IsAsciiSlugByte(b) || b == (byte)':';

    /// <summary>True for elements whose content is rawtext / RCDATA and must not be scanned for tags.</summary>
    /// <param name="name">Tag name bytes.</param>
    /// <returns><see langword="true"/> for <c>script</c>, <c>style</c>, <c>title</c>, <c>textarea</c>.</returns>
    private static bool IsRawTextElement(ReadOnlySpan<byte> name) =>
        AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "script"u8)
        || AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "style"u8)
        || AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "title"u8)
        || AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "textarea"u8);

    /// <summary>Clears the current-token state before scanning for the next tag.</summary>
    private void Reset()
    {
        Name = default;
        Attributes = default;
        RawText = default;
        IsEndTag = false;
        IsSelfClosing = false;
    }

    /// <summary>Skips an HTML comment that begins at <paramref name="lt"/>.</summary>
    /// <param name="lt">Offset of the comment's opening <c>&lt;</c>.</param>
    /// <returns><see langword="true"/> when the comment closed; <see langword="false"/> when the input ended mid-comment.</returns>
    private bool SkipComment(int lt)
    {
        var commentEnd = _html[(lt + CommentOpenLength)..].IndexOf("-->"u8);
        if (commentEnd < 0)
        {
            _pos = _html.Length;
            return false;
        }

        _pos = lt + CommentOpenLength + commentEnd + CommentCloseLength;
        return true;
    }

    /// <summary>Reads the tag whose name begins at <paramref name="nameStart"/> and updates the token state.</summary>
    /// <param name="lt">Offset of the tag's opening <c>&lt;</c>.</param>
    /// <param name="nameStart">Offset of the first byte of the tag name.</param>
    /// <param name="isEnd">Whether the tag is an end tag.</param>
    /// <returns><see langword="true"/> when a tag was produced; <see langword="false"/> on an unterminated tag.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Tokenizer step; the branching tracks tag shapes (end / self-closing / rawtext), not nested logic.")]
    private bool ReadTag(int lt, int nameStart, bool isEnd)
    {
        var cursor = nameStart;
        while (cursor < _html.Length && IsNameChar(_html[cursor]))
        {
            cursor++;
        }

        var name = _html[nameStart..cursor];
        var gt = FindTagEnd(cursor);
        if (gt < 0)
        {
            _pos = _html.Length;
            return false;
        }

        var selfClose = !isEnd && gt > cursor && _html[gt - 1] == (byte)'/';
        var attrEnd = selfClose ? gt - 1 : gt;
        Name = name;
        Attributes = cursor < attrEnd ? _html[cursor..attrEnd] : default;
        IsEndTag = isEnd;
        IsSelfClosing = selfClose;
        TagStart = lt;

        if (!isEnd && !selfClose && IsRawTextElement(name))
        {
            return ReadRawTextBody(gt + 1, name);
        }

        TagEnd = gt + 1;
        _pos = gt + 1;
        return true;
    }

    /// <summary>Captures the body of a rawtext element and advances past its end tag.</summary>
    /// <param name="contentStart">Offset of the first byte after the element's start tag.</param>
    /// <param name="name">The element's tag name.</param>
    /// <returns>Always <see langword="true"/> — the start tag is the produced token.</returns>
    private bool ReadRawTextBody(int contentStart, ReadOnlySpan<byte> name)
    {
        var closeAbs = AuditText.FindCloseTag(_html, contentStart, name);
        if (closeAbs < 0)
        {
            RawText = _html[contentStart..];
            TagEnd = _html.Length;
            _pos = _html.Length;
            return true;
        }

        RawText = _html[contentStart..closeAbs];
        var afterClose = FindTagEnd(closeAbs + EndTagPrefixLength + name.Length);
        _pos = afterClose < 0 ? _html.Length : afterClose + 1;
        TagEnd = _pos;
        return true;
    }

    /// <summary>Tests whether <paramref name="probe"/> occurs at <paramref name="index"/> in the source.</summary>
    /// <param name="index">Start offset.</param>
    /// <param name="probe">Bytes to compare.</param>
    /// <returns><see langword="true"/> on an exact prefix match.</returns>
    private readonly bool StartsWithAt(int index, ReadOnlySpan<byte> probe) =>
        _html.Length - index >= probe.Length && _html.Slice(index, probe.Length).SequenceEqual(probe);

    /// <summary>Finds the index of the unquoted <c>&gt;</c> that closes a tag, starting at <paramref name="from"/>.</summary>
    /// <param name="from">Offset just past the tag name.</param>
    /// <returns>The index of the closing bracket, or <c>-1</c> when the tag is unterminated.</returns>
    private readonly int FindTagEnd(int from)
    {
        byte quote = 0;
        for (var i = from; i < _html.Length; i++)
        {
            var c = _html[i];
            if (quote != 0)
            {
                if (c == quote)
                {
                    quote = 0;
                }

                continue;
            }

            if (c is (byte)'"' or (byte)'\'')
            {
                quote = c;
            }
            else if (c == (byte)'>')
            {
                return i;
            }
        }

        return -1;
    }
}
