// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>PHP lexer.</summary>
/// <remarks>
/// Single-state lexer that treats the whole document as PHP source. The
/// <c>&lt;?php</c> / <c>&lt;?=</c> open tag and <c>?&gt;</c> close tag classify as
/// preprocessor markers; everything between them follows the brace-style
/// C-family shape with PHP's <c>$variable</c> sigil and <c>#</c>/<c>//</c>/<c>/* */</c>
/// comment forms.
/// </remarks>
public static class PhpLexer
{
    /// <summary>Length of the <c>&lt;?php</c> open tag.</summary>
    private const int PhpOpenTagLength = 5;

    /// <summary>Length of the <c>&lt;?=</c> short-echo open tag.</summary>
    private const int ShortEchoTagLength = 3;

    /// <summary>Length of the bare <c>&lt;?</c> open tag and the <c>?&gt;</c> close tag.</summary>
    private const int BareTagLength = 2;

    /// <summary>General-keyword set (case-insensitive — entries are lowercase).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateIgnoreCase(
        [.. "if"u8],
        [.. "else"u8],
        [.. "elseif"u8],
        [.. "endif"u8],
        [.. "for"u8],
        [.. "foreach"u8],
        [.. "endfor"u8],
        [.. "endforeach"u8],
        [.. "while"u8],
        [.. "endwhile"u8],
        [.. "do"u8],
        [.. "switch"u8],
        [.. "case"u8],
        [.. "default"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "return"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "new"u8],
        [.. "as"u8],
        [.. "instanceof"u8],
        [.. "yield"u8],
        [.. "match"u8],
        [.. "fn"u8],
        [.. "echo"u8],
        [.. "print"u8],
        [.. "include"u8],
        [.. "include_once"u8],
        [.. "require"u8],
        [.. "require_once"u8],
        [.. "use"u8],
        [.. "namespace"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "xor"u8]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateIgnoreCase(
        [.. "int"u8],
        [.. "integer"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "bool"u8],
        [.. "boolean"u8],
        [.. "string"u8],
        [.. "array"u8],
        [.. "object"u8],
        [.. "void"u8],
        [.. "mixed"u8],
        [.. "iterable"u8],
        [.. "callable"u8],
        [.. "self"u8],
        [.. "parent"u8],
        [.. "static"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateIgnoreCase(
        [.. "function"u8],
        [.. "class"u8],
        [.. "interface"u8],
        [.. "trait"u8],
        [.. "enum"u8],
        [.. "extends"u8],
        [.. "implements"u8],
        [.. "abstract"u8],
        [.. "final"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "readonly"u8],
        [.. "const"u8],
        [.. "var"u8],
        [.. "global"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateIgnoreCase(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<=>"u8],
        [.. "**="u8],
        [.. "??="u8],
        [.. "<<="u8],
        [.. ">>="u8],
        [.. "=="u8],
        [.. "==="u8],
        [.. "!="u8],
        [.. "!=="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "??"u8],
        [.. "?->"u8],
        [.. "->"u8],
        [.. "::"u8],
        [.. "=>"u8],
        [.. "**"u8],
        [.. ".="u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
        [.. "&="u8],
        [.. "|="u8],
        [.. "^="u8],
        [.. "."u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "&"u8],
        [.. "|"u8],
        [.. "^"u8],
        [.. "!"u8],
        [.. "~"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "?"u8]
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("aAbBcCdDeEfFiIlLmMnNoOpPrRsStTuUwWxXyY"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("aAbBcCdDfFiIoOpPsSvVmM"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("aAcCeEfFgGiIpPrRtTvV"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tTfFnN"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?:.@"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,\\"u8);

    /// <summary>First-byte set for the variable rule (<c>$</c> sigil).</summary>
    private static readonly SearchValues<byte> DollarFirst = SearchValues.Create("$"u8);

    /// <summary>First-byte set for the PHP open / close tags.</summary>
    private static readonly SearchValues<byte> AngleFirst = SearchValues.Create("<?"u8);

    /// <summary>First-byte set for the hash-comment rule.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>Gets the singleton PHP lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the PHP lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        var coreRules = CFamilyRules.Build(config);

        // Front rules: PHP open/close tags + variable sigil + # line comment.
        LexerRule[] frontRules =
        [
            new(MatchPhpTag, TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = AngleFirst },
            new(static slice => TokenMatchers.MatchPrefixedRun(slice, (byte)'$', TokenMatchers.AsciiIdentifierContinue), TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = DollarFirst },
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst }
        ];

        var allRules = new LexerRule[frontRules.Length + coreRules.Length];
        Array.Copy(frontRules, 0, allRules, 0, frontRules.Length);
        Array.Copy(coreRules, 0, allRules, frontRules.Length, coreRules.Length);

        return new(LanguageRuleBuilder.BuildSingleState(allRules));
    }

    /// <summary>Matches a PHP open tag (<c>&lt;?php</c> / <c>&lt;?=</c>) or close tag (<c>?&gt;</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchPhpTag(ReadOnlySpan<byte> slice) => slice switch
    {
        [(byte)'<', (byte)'?', (byte)'p', (byte)'h', (byte)'p', ..] => PhpOpenTagLength,
        [(byte)'<', (byte)'?', (byte)'=', ..] => ShortEchoTagLength,
        [(byte)'<', (byte)'?', ..] => BareTagLength,
        [(byte)'?', (byte)'>', ..] => BareTagLength,
        _ => 0
    };
}
