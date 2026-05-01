// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Reusable C# rule list factory, shared between <see cref="CSharpLexer"/>
/// and <see cref="RazorLexer"/>.
/// </summary>
/// <remarks>
/// Extracted as a helper so embedded-language scenarios (Razor's
/// <c>@code { ... }</c> blocks, Markdown-in-HTML, future Blazor
/// components) all classify C# tokens the same way without duplicating
/// the rule list.
/// </remarks>
internal static partial class CSharpRules
{
    /// <summary>Pattern alternation for declaration keywords; extracted so the <c>[GeneratedRegex]</c> attribute line stays under the line-length cap.</summary>
    private const string DeclarationKeywords =
        "class|struct|interface|enum|record|delegate|namespace|using|var|let|const|" +
        "readonly|static|abstract|sealed|virtual|override|partial|public|private|" +
        "protected|internal|extern|new|this|base|file|required";

    /// <summary>Pattern alternation for general keywords.</summary>
    private const string GeneralKeywords =
        "if|else|for|foreach|while|do|return|switch|case|default|break|continue|" +
        "goto|try|catch|finally|throw|await|async|yield|in|out|ref|params|where|" +
        "select|from|join|orderby|group|by|into|on|equals|is|as|typeof|sizeof|" +
        "stackalloc|nameof|when|with|fixed|lock|unsafe|operator|implicit|explicit|" +
        "checked|unchecked|global";

    /// <summary>First-char set for whitespace runs (C# tokens; newlines are not consumed by this regex).</summary>
    private static readonly SearchValues<char> WhitespaceFirst = SearchValues.Create(" \t");

    /// <summary>First-char set for preprocessor directives (line may start with leading whitespace before <c>#</c>).</summary>
    private static readonly SearchValues<char> PreprocessorFirst = SearchValues.Create(" \t#");

    /// <summary>First-char set for the <c>true</c> / <c>false</c> / <c>null</c> keyword constants.</summary>
    private static readonly SearchValues<char> KeywordConstantFirst = SearchValues.Create("tfn");

    /// <summary>First-char set for built-in type keywords.</summary>
    private static readonly SearchValues<char> KeywordTypeFirst = SearchValues.Create("bsuilfdcovn");

    /// <summary>First-char set for declaration keywords.</summary>
    private static readonly SearchValues<char> KeywordDeclarationFirst = SearchValues.Create("csiernduvlaoptbf");

    /// <summary>First-char set for general keywords.</summary>
    private static readonly SearchValues<char> KeywordFirst = SearchValues.Create("iefwdrscbgtayophjnlu");

    /// <summary>First-char set for identifiers (ASCII letters + underscore).</summary>
    private static readonly SearchValues<char> IdentifierFirst = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_");

    /// <summary>First-char set for operator tokens.</summary>
    private static readonly SearchValues<char> OperatorFirst = SearchValues.Create("?=<>!&|+-*/%^~:");

    /// <summary>Builds the C# rule list. Order matters — doc comments must precede line comments.</summary>
    /// <returns>Ordered rule list classifying C# tokens with Pygments-shape CSS classes.</returns>
    public static LexerRule[] Build() =>
    [
        new(WhitespaceRegex(), TokenClass.Whitespace, NextState: null) { FirstChars = WhitespaceFirst },

        // /// must precede // so the doc-comment longer prefix wins.
        new(DocCommentRegex(), TokenClass.CommentSpecial, NextState: null) { FirstChars = LanguageCommon.SlashFirst },
        new(LanguageCommon.LineComment(), TokenClass.CommentSingle, NextState: null) { FirstChars = LanguageCommon.SlashFirst },
        new(LanguageCommon.BlockComment(), TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.SlashFirst },
        new(PreprocessorRegex(), TokenClass.CommentPreproc, NextState: null) { FirstChars = PreprocessorFirst },
        new(VerbatimStringRegex(), TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.AtFirst },
        new(LanguageCommon.DoubleQuotedStringWithEscapes(), TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst },
        new(CharRegex(), TokenClass.StringSingle, NextState: null) { FirstChars = LanguageCommon.SingleQuoteFirst },
        new(HexNumberRegex(), TokenClass.NumberHex, NextState: null) { FirstChars = LanguageCommon.HexFirst },
        new(FloatNumberRegex(), TokenClass.NumberFloat, NextState: null) { FirstChars = LanguageCommon.DigitFirst },
        new(IntNumberRegex(), TokenClass.NumberInteger, NextState: null) { FirstChars = LanguageCommon.IntegerFirst },
        new(KeywordConstantRegex(), TokenClass.KeywordConstant, NextState: null) { FirstChars = KeywordConstantFirst },
        new(KeywordTypeRegex(), TokenClass.KeywordType, NextState: null) { FirstChars = KeywordTypeFirst },
        new(KeywordDeclarationRegex(), TokenClass.KeywordDeclaration, NextState: null) { FirstChars = KeywordDeclarationFirst },
        new(KeywordRegex(), TokenClass.Keyword, NextState: null) { FirstChars = KeywordFirst },
        new(IdentifierRegex(), TokenClass.Name, NextState: null) { FirstChars = IdentifierFirst },
        new(OperatorRegex(), TokenClass.Operator, NextState: null) { FirstChars = OperatorFirst },
        new(LanguageCommon.CCurlyPunctuation(), TokenClass.Punctuation, NextState: null) { FirstChars = LanguageCommon.CCurlyPunctuationFirst },
    ];

    [GeneratedRegex(@"\G[ \t]+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\G///[^\r\n]*", RegexOptions.Compiled)]
    private static partial Regex DocCommentRegex();

    [GeneratedRegex(@"\G^[ \t]*#[^\r\n]*", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex PreprocessorRegex();

    [GeneratedRegex("\\G@\"(?:\"\"|[^\"])*\"", RegexOptions.Compiled)]
    private static partial Regex VerbatimStringRegex();

    [GeneratedRegex(@"\G'(?:\\.|[^'\\])'", RegexOptions.Compiled)]
    private static partial Regex CharRegex();

    [GeneratedRegex(@"\G0[xX][0-9a-fA-F_]+[uUlL]*", RegexOptions.Compiled)]
    private static partial Regex HexNumberRegex();

    [GeneratedRegex(@"\G[0-9]+\.[0-9]+(?:[eE][+-]?[0-9]+)?[fFdDmM]?", RegexOptions.Compiled)]
    private static partial Regex FloatNumberRegex();

    [GeneratedRegex(@"\G[0-9_]+[uUlL]*", RegexOptions.Compiled)]
    private static partial Regex IntNumberRegex();

    [GeneratedRegex(@"\G(?:true|false|null)\b", RegexOptions.Compiled)]
    private static partial Regex KeywordConstantRegex();

    [GeneratedRegex(@"\G(?:bool|byte|sbyte|short|ushort|int|uint|long|ulong|float|double|decimal|char|string|object|void|nint|nuint)\b", RegexOptions.Compiled)]
    private static partial Regex KeywordTypeRegex();

    [GeneratedRegex(@"\G(?:" + DeclarationKeywords + @")\b", RegexOptions.Compiled)]
    private static partial Regex KeywordDeclarationRegex();

    [GeneratedRegex(@"\G(?:" + GeneralKeywords + @")\b", RegexOptions.Compiled)]
    private static partial Regex KeywordRegex();

    [GeneratedRegex(@"\G[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"\G(?:\?\?=?|=>|<<=|>>=|<=|>=|==|!=|&&|\|\||\+\+|--|->|<<|>>|\?\.|::|<|>|\+|-|\*|/|%|&|\||\^|!|~|=|\?)", RegexOptions.Compiled)]
    private static partial Regex OperatorRegex();
}
