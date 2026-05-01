// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>YAML lexer.</summary>
/// <remarks>
/// Pragmatic subset of Pygments' <c>YamlLexer</c>: comments, mapping
/// keys (followed by <c>:</c>), document separators, anchors / aliases,
/// quoted strings, numbers, and literal constants. Block scalars
/// (<c>|</c>, <c>&gt;</c>) are recognised at the indicator and their
/// payloads fall through as plain text — fine for typical config docs.
/// </remarks>
public static partial class YamlLexer
{
    /// <summary>First-char set for whitespace runs.</summary>
    private static readonly SearchValues<char> WhitespaceFirst = SearchValues.Create(" \t\r\n");

    /// <summary>First-char set for the <c>#</c> line-comment indicator.</summary>
    private static readonly SearchValues<char> CommentFirst = SearchValues.Create("#");

    /// <summary>First-char set for YAML anchors (<c>&amp;name</c>).</summary>
    private static readonly SearchValues<char> AnchorFirst = SearchValues.Create("&");

    /// <summary>First-char set for YAML aliases (<c>*name</c>).</summary>
    private static readonly SearchValues<char> AliasFirst = SearchValues.Create("*");

    /// <summary>First-char set for YAML tag indicators (<c>!</c> / <c>!!</c>).</summary>
    private static readonly SearchValues<char> TagFirst = SearchValues.Create("!");

    /// <summary>First-char set for double-quoted strings (and quoted keys).</summary>
    private static readonly SearchValues<char> DoubleQuoteFirst = SearchValues.Create("\"");

    /// <summary>First-char set for single-quoted strings.</summary>
    private static readonly SearchValues<char> SingleQuoteFirst = SearchValues.Create("'");

    /// <summary>First-char set for plain identifiers and plain keys (letters, underscore).</summary>
    private static readonly SearchValues<char> IdentifierFirst = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_");

    /// <summary>First-char set for block scalar indicators (<c>|</c>, <c>&gt;</c>).</summary>
    private static readonly SearchValues<char> BlockScalarFirst = SearchValues.Create("|>");

    /// <summary>First-char set for the case-insensitive YAML keyword constants (<c>true</c>, <c>false</c>, <c>null</c>, <c>yes</c>, <c>no</c>, <c>on</c>, <c>off</c>, <c>~</c>).</summary>
    private static readonly SearchValues<char> KeywordConstantFirst = SearchValues.Create("tTfFnNyYoO~");

    /// <summary>First-char set for numeric tokens (digits + leading minus).</summary>
    private static readonly SearchValues<char> NumberFirst = SearchValues.Create("-0123456789");

    /// <summary>First-char set for flow-style structural punctuation.</summary>
    private static readonly SearchValues<char> PunctuationFirst = SearchValues.Create("{}[],:");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "yaml",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] =
            [
                new(WhitespaceRegex(), TokenClass.Whitespace, NextState: null) { FirstChars = WhitespaceFirst },
                new(CommentRegex(), TokenClass.CommentSingle, NextState: null) { FirstChars = CommentFirst },
                new(DocumentSeparatorRegex(), TokenClass.CommentPreproc, NextState: null),
                new(AnchorRegex(), TokenClass.NameClass, NextState: null) { FirstChars = AnchorFirst },
                new(AliasRegex(), TokenClass.NameClass, NextState: null) { FirstChars = AliasFirst },
                new(TagRegex(), TokenClass.NameAttribute, NextState: null) { FirstChars = TagFirst },
                new(KeyQuotedRegex(), TokenClass.NameAttribute, NextState: null) { FirstChars = DoubleQuoteFirst },
                new(KeyPlainRegex(), TokenClass.NameAttribute, NextState: null) { FirstChars = IdentifierFirst },
                new(StringDoubleRegex(), TokenClass.StringDouble, NextState: null) { FirstChars = DoubleQuoteFirst },
                new(StringSingleRegex(), TokenClass.StringSingle, NextState: null) { FirstChars = SingleQuoteFirst },
                new(BlockScalarIndicatorRegex(), TokenClass.Punctuation, NextState: null) { FirstChars = BlockScalarFirst },
                new(KeywordConstantRegex(), TokenClass.KeywordConstant, NextState: null) { FirstChars = KeywordConstantFirst },
                new(FloatRegex(), TokenClass.NumberFloat, NextState: null) { FirstChars = NumberFirst },
                new(IntegerRegex(), TokenClass.NumberInteger, NextState: null) { FirstChars = NumberFirst },
                new(BulletRegex(), TokenClass.Punctuation, NextState: null),
                new(PunctuationRegex(), TokenClass.Punctuation, NextState: null) { FirstChars = PunctuationFirst },
                new(IdentifierRegex(), TokenClass.Name, NextState: null) { FirstChars = IdentifierFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));

    [GeneratedRegex(@"\G[ \t\r\n]+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\G\#[^\r\n]*", RegexOptions.Compiled)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\G---|\.\.\.", RegexOptions.Compiled)]
    private static partial Regex DocumentSeparatorRegex();

    [GeneratedRegex(@"\G&[A-Za-z0-9_-]+", RegexOptions.Compiled)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"\G\*[A-Za-z0-9_-]+", RegexOptions.Compiled)]
    private static partial Regex AliasRegex();

    [GeneratedRegex(@"\G!!?[A-Za-z0-9_/-]*", RegexOptions.Compiled)]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\G\"(?:\\\\.|[^\"\\\\])*\"(?=\\s*:)", RegexOptions.Compiled)]
    private static partial Regex KeyQuotedRegex();

    [GeneratedRegex(@"\G[A-Za-z_][A-Za-z0-9_.\-]*(?=\s*:(?:\s|$))", RegexOptions.Compiled)]
    private static partial Regex KeyPlainRegex();

    [GeneratedRegex("\\G\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled)]
    private static partial Regex StringDoubleRegex();

    [GeneratedRegex(@"\G'(?:''|[^'])*'", RegexOptions.Compiled)]
    private static partial Regex StringSingleRegex();

    [GeneratedRegex(@"\G[|>][+-]?[0-9]?", RegexOptions.Compiled)]
    private static partial Regex BlockScalarIndicatorRegex();

    [GeneratedRegex(@"\G(?:true|false|null|yes|no|on|off|~)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex KeywordConstantRegex();

    [GeneratedRegex(@"\G-?[0-9]+\.[0-9]+(?:[eE][+-]?[0-9]+)?", RegexOptions.Compiled)]
    private static partial Regex FloatRegex();

    [GeneratedRegex(@"\G-?[0-9]+", RegexOptions.Compiled)]
    private static partial Regex IntegerRegex();

    [GeneratedRegex(@"\G^\s*-\s", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\G[\{\}\[\],:]", RegexOptions.Compiled)]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\G[A-Za-z_][A-Za-z0-9_.-]*", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex();
}
