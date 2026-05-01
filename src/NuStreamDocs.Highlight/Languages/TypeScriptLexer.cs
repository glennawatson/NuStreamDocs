// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>TypeScript lexer (also covers JavaScript via <see cref="JavaScriptLexer"/> reuse).</summary>
/// <remarks>
/// Modelled on Pygments' <c>TypeScriptLexer</c> shape. Single-state
/// machine — the language is regular enough that the cursor advances
/// past one token per cycle without ever stacking states. Template
/// literals and JSX are deliberately deferred; everything else
/// (strings, regex, comments, numbers, keywords, operators) lights
/// up.
/// </remarks>
public static partial class TypeScriptLexer
{
    /// <summary>Pattern alternation for declaration keywords; extracted so the <c>[GeneratedRegex]</c> attribute line stays under the line-length cap.</summary>
    private const string DeclarationKeywords =
        "var|let|const|function|class|interface|enum|type|namespace|module|export|" +
        "import|from|as|declare|abstract|public|private|protected|static|async";

    /// <summary>Pattern alternation for general keywords.</summary>
    private const string GeneralKeywords =
        "if|else|for|while|do|return|switch|case|default|break|continue|throw|" +
        "try|catch|finally|new|delete|in|of|instanceof|typeof|void|yield|await|" +
        "this|super|extends|implements";

    /// <summary>Pattern alternation for operators (long; covers TS-specific punctuation like <c>?.</c> and <c>??</c>).</summary>
    private const string Operators =
        @"\?\?=?|\?\.|=>|\.\.\.|<<=|>>>=|>>=|<=|>=|===|!==|==|!=|&&=|\|\|=|\?\?|" +
        @"&&|\|\||\+\+|--|<<|>>>|>>|\+=|-=|\*=|/=|%=|&=|\|=|\^=|\*\*=|\*\*|" +
        @"<|>|\+|-|\*|/|%|&|\||\^|!|~|=|\?";

    /// <summary>First-char set for backtick template literals.</summary>
    private static readonly SearchValues<char> BacktickFirst = SearchValues.Create("`");

    /// <summary>First-char set for keyword constants (<c>true</c> / <c>false</c> / <c>null</c> / <c>undefined</c> / <c>NaN</c> / <c>Infinity</c>).</summary>
    private static readonly SearchValues<char> KeywordConstantFirst = SearchValues.Create("tfnuNI");

    /// <summary>First-char set for built-in type keywords.</summary>
    private static readonly SearchValues<char> KeywordTypeFirst = SearchValues.Create("abnsvuokr");

    /// <summary>First-char set for declaration keywords.</summary>
    private static readonly SearchValues<char> KeywordDeclarationFirst = SearchValues.Create("vlcfietnmpsad");

    /// <summary>First-char set for general keywords.</summary>
    private static readonly SearchValues<char> KeywordFirst = SearchValues.Create("iefwdrscbtnovya");

    /// <summary>First-char set for identifiers (ASCII letters, underscore, dollar).</summary>
    private static readonly SearchValues<char> IdentifierFirst = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$");

    /// <summary>First-char set for operator tokens.</summary>
    private static readonly SearchValues<char> OperatorFirst = SearchValues.Create("?=.<>!&|+-*/%^~");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the rule list. Exposed internal so <see cref="JavaScriptLexer"/> can reuse the patterns under a different language name.</summary>
    /// <param name="languageHint">Diagnostic-only language tag.</param>
    /// <returns>Ordered rule list.</returns>
    internal static LexerRule[] BuildRules(string languageHint)
    {
        _ = languageHint;
        return
        [
            new(LanguageCommon.WhitespaceWithNewlines(), TokenClass.Whitespace, NextState: null) { FirstChars = LanguageCommon.WhitespaceWithNewlinesFirst },
            new(LanguageCommon.LineComment(), TokenClass.CommentSingle, NextState: null) { FirstChars = LanguageCommon.SlashFirst },
            new(LanguageCommon.BlockComment(), TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.SlashFirst },
            new(TemplateStringRegex(), TokenClass.StringDouble, NextState: null) { FirstChars = BacktickFirst },
            new(LanguageCommon.DoubleQuotedStringWithEscapes(), TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst },
            new(SingleStringRegex(), TokenClass.StringSingle, NextState: null) { FirstChars = LanguageCommon.SingleQuoteFirst },
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
    }

    /// <summary>Builds the lexer.</summary>
    /// <returns>Configured lexer.</returns>
    private static Lexer Build()
    {
        var states = new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = BuildRules("typescript"),
        }.ToFrozenDictionary(StringComparer.Ordinal);
        return new("typescript", states);
    }

    [GeneratedRegex(@"\G`(?:\\.|[^`\\])*`", RegexOptions.Compiled)]
    private static partial Regex TemplateStringRegex();

    [GeneratedRegex(@"\G'(?:\\.|[^'\\])*'", RegexOptions.Compiled)]
    private static partial Regex SingleStringRegex();

    [GeneratedRegex(@"\G0[xX][0-9a-fA-F_]+n?", RegexOptions.Compiled)]
    private static partial Regex HexNumberRegex();

    [GeneratedRegex(@"\G[0-9]+\.[0-9]+(?:[eE][+-]?[0-9]+)?", RegexOptions.Compiled)]
    private static partial Regex FloatNumberRegex();

    [GeneratedRegex(@"\G[0-9_]+n?", RegexOptions.Compiled)]
    private static partial Regex IntNumberRegex();

    [GeneratedRegex(@"\G(?:true|false|null|undefined|NaN|Infinity)\b", RegexOptions.Compiled)]
    private static partial Regex KeywordConstantRegex();

    [GeneratedRegex(@"\G(?:any|boolean|number|string|void|never|unknown|object|symbol|bigint|readonly|keyof)\b", RegexOptions.Compiled)]
    private static partial Regex KeywordTypeRegex();

    [GeneratedRegex(@"\G(?:" + DeclarationKeywords + @")\b", RegexOptions.Compiled)]
    private static partial Regex KeywordDeclarationRegex();

    [GeneratedRegex(@"\G(?:" + GeneralKeywords + @")\b", RegexOptions.Compiled)]
    private static partial Regex KeywordRegex();

    [GeneratedRegex(@"\G[A-Za-z_$][A-Za-z0-9_$]*", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"\G(?:" + Operators + ")", RegexOptions.Compiled)]
    private static partial Regex OperatorRegex();
}
