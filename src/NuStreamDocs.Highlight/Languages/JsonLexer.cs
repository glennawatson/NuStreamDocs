// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>JSON lexer.</summary>
/// <remarks>
/// Strings, numbers, the literal keywords <c>true</c> / <c>false</c> /
/// <c>null</c>, and structural punctuation. Pygments classifies
/// property keys (strings followed by <c>:</c>) under <c>Name.Tag</c>
/// (CSS class <c>nt</c>); we fold those into <see cref="TokenClass.NameAttribute"/>
/// (CSS class <c>na</c>) which existing themes also style.
/// </remarks>
public static partial class JsonLexer
{
    /// <summary>First-char set for whitespace runs.</summary>
    private static readonly SearchValues<char> WhitespaceFirst = SearchValues.Create(" \t\r\n");

    /// <summary>First-char set for string-shaped tokens (keys + values).</summary>
    private static readonly SearchValues<char> QuoteFirst = SearchValues.Create("\"");

    /// <summary>First-char set for numeric tokens (digits + leading minus).</summary>
    private static readonly SearchValues<char> NumberFirst = SearchValues.Create("-0123456789");

    /// <summary>First-char set for the <c>true</c> / <c>false</c> / <c>null</c> keyword constants.</summary>
    private static readonly SearchValues<char> KeywordFirst = SearchValues.Create("tfn");

    /// <summary>First-char set for structural punctuation.</summary>
    private static readonly SearchValues<char> PunctuationFirst = SearchValues.Create("{}[],:");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "json",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] =
            [
                new(WhitespaceRegex(), TokenClass.Whitespace, NextState: null) { FirstChars = WhitespaceFirst },
                new(KeyRegex(), TokenClass.NameAttribute, NextState: null) { FirstChars = QuoteFirst },
                new(StringRegex(), TokenClass.StringDouble, NextState: null) { FirstChars = QuoteFirst },
                new(FloatRegex(), TokenClass.NumberFloat, NextState: null) { FirstChars = NumberFirst },
                new(IntegerRegex(), TokenClass.NumberInteger, NextState: null) { FirstChars = NumberFirst },
                new(KeywordConstantRegex(), TokenClass.KeywordConstant, NextState: null) { FirstChars = KeywordFirst },
                new(PunctuationRegex(), TokenClass.Punctuation, NextState: null) { FirstChars = PunctuationFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));

    [GeneratedRegex(@"\G[ \t\r\n]+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("\\G\"(?:\\\\.|[^\"\\\\])*\"(?=\\s*:)", RegexOptions.Compiled)]
    private static partial Regex KeyRegex();

    [GeneratedRegex("\\G\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled)]
    private static partial Regex StringRegex();

    [GeneratedRegex(@"\G-?[0-9]+\.[0-9]+(?:[eE][+-]?[0-9]+)?", RegexOptions.Compiled)]
    private static partial Regex FloatRegex();

    [GeneratedRegex(@"\G-?[0-9]+", RegexOptions.Compiled)]
    private static partial Regex IntegerRegex();

    [GeneratedRegex(@"\G(?:true|false|null)\b", RegexOptions.Compiled)]
    private static partial Regex KeywordConstantRegex();

    [GeneratedRegex(@"\G[\{\}\[\],:]", RegexOptions.Compiled)]
    private static partial Regex PunctuationRegex();
}
