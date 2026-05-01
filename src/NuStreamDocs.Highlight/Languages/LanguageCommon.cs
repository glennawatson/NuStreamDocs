// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Cross-language SearchValues and <c>[GeneratedRegex]</c> patterns
/// shared by the bundled lexers. Keeps the per-language rule files
/// from each declaring an identical copy of the same byte-sets and
/// keeping the JIT/AOT story unchanged: every regex on this class is
/// still source-generated, every <see cref="SearchValues{T}"/>
/// instance is still a static-readonly frozen lookup table.
/// </summary>
internal static partial class LanguageCommon
{
    /// <summary>First-char set for a leading <c>/</c> (line / block / doc comments).</summary>
    public static readonly SearchValues<char> SlashFirst = SearchValues.Create("/");

    /// <summary>First-char set for single-quoted character / string literals.</summary>
    public static readonly SearchValues<char> SingleQuoteFirst = SearchValues.Create("'");

    /// <summary>First-char set for double-quoted string literals.</summary>
    public static readonly SearchValues<char> DoubleQuoteFirst = SearchValues.Create("\"");

    /// <summary>First-char set for whitespace runs that include line terminators (TypeScript / XML / Razor flavour).</summary>
    public static readonly SearchValues<char> WhitespaceWithNewlinesFirst = SearchValues.Create(" \t\r\n");

    /// <summary>First-char set for hexadecimal numeric literals.</summary>
    public static readonly SearchValues<char> HexFirst = SearchValues.Create("0");

    /// <summary>First-char set for decimal numeric literals (digits only).</summary>
    public static readonly SearchValues<char> DigitFirst = SearchValues.Create("0123456789");

    /// <summary>First-char set for integer literals (digits + leading underscore allowed by the regex).</summary>
    public static readonly SearchValues<char> IntegerFirst = SearchValues.Create("0123456789_");

    /// <summary>First-char set for C-curly structural punctuation (parens, brackets, braces, semicolon, comma, dot, colon).</summary>
    public static readonly SearchValues<char> CCurlyPunctuationFirst = SearchValues.Create("(){}[];,.:");

    /// <summary>First-char set for an opening angle bracket (XML / Razor tag start).</summary>
    public static readonly SearchValues<char> AngleOpenFirst = SearchValues.Create("<");

    /// <summary>First-char set for a closing angle bracket.</summary>
    public static readonly SearchValues<char> AngleCloseFirst = SearchValues.Create(">");

    /// <summary>First-char set for the equals sign in attribute syntax.</summary>
    public static readonly SearchValues<char> EqualsFirst = SearchValues.Create("=");

    /// <summary>First-char set for SGML / XML entity references (<c>&amp;name;</c>).</summary>
    public static readonly SearchValues<char> EntityFirst = SearchValues.Create("&");

    /// <summary>First-char set for the at-sign Razor / verbatim-string trigger.</summary>
    public static readonly SearchValues<char> AtFirst = SearchValues.Create("@");

    /// <summary>First-char set for an XML / Razor tag name (ASCII letters and underscore).</summary>
    public static readonly SearchValues<char> TagNameFirst = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_");

    /// <summary>First-char set for an XML / Razor attribute name (ASCII letters, underscore, colon).</summary>
    public static readonly SearchValues<char> AttributeNameFirst = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_:");

    /// <summary>Whitespace run that includes line terminators — TypeScript / XML / Razor flavour.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G[ \t\r\n]+", RegexOptions.Compiled)]
    public static partial Regex WhitespaceWithNewlines();

    /// <summary>C-style line comment — <c>//</c> to end of line.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G//[^\r\n]*", RegexOptions.Compiled)]
    public static partial Regex LineComment();

    /// <summary>C-style block comment — <c>/* ... */</c> non-greedy.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G/\*[\s\S]*?\*/", RegexOptions.Compiled)]
    public static partial Regex BlockComment();

    /// <summary>Double-quoted string with backslash escapes (C# / TypeScript flavour).</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex("\\G\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled)]
    public static partial Regex DoubleQuotedStringWithEscapes();

    /// <summary>Single-quoted no-escape string (XML / Razor attribute value).</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G'[^']*'", RegexOptions.Compiled)]
    public static partial Regex SingleQuotedStringNoEscape();

    /// <summary>Double-quoted no-escape string (XML / Razor attribute value).</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex("\\G\"[^\"]*\"", RegexOptions.Compiled)]
    public static partial Regex DoubleQuotedStringNoEscape();

    /// <summary>C-curly structural punctuation: <c>(){}[];,.:</c>.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G[\(\)\{\}\[\];,.:]", RegexOptions.Compiled)]
    public static partial Regex CCurlyPunctuation();

    /// <summary>Single open-angle character — XML tag start.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G<", RegexOptions.Compiled)]
    public static partial Regex AngleOpen();

    /// <summary>Open-angle followed by a slash — XML closing tag start.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G</", RegexOptions.Compiled)]
    public static partial Regex AngleOpenSlash();

    /// <summary>Single close-angle character — XML tag end.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G>", RegexOptions.Compiled)]
    public static partial Regex AngleClose();

    /// <summary>Self-closing tag terminator — <c>/&gt;</c>.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G/>", RegexOptions.Compiled)]
    public static partial Regex SelfClose();

    /// <summary>Equals sign — XML / Razor attribute separator.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G=", RegexOptions.Compiled)]
    public static partial Regex EqualsSign();

    /// <summary>SGML / XML entity reference (e.g. <c>&amp;amp;</c>).</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G&[#A-Za-z0-9]+;", RegexOptions.Compiled)]
    public static partial Regex EntityReference();

    /// <summary>XML / Razor attribute name (ASCII letters, digits, <c>_</c>, <c>:</c>, <c>.</c>, <c>-</c>) followed by <c>=</c>.</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G[A-Za-z_:][A-Za-z0-9_:.-]*(?=\s*=)", RegexOptions.Compiled)]
    public static partial Regex AttributeName();

    /// <summary>XML / Razor tag name (ASCII letters and digits with optional <c>:</c>, <c>.</c>, <c>-</c>).</summary>
    /// <returns>A source-generated regex.</returns>
    [GeneratedRegex(@"\G[A-Za-z_][A-Za-z0-9_:.-]*", RegexOptions.Compiled)]
    public static partial Regex TagName();
}
