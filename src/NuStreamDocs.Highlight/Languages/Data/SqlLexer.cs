// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Data;

/// <summary>SQL lexer.</summary>
/// <remarks>
/// Case-insensitive keyword tables (every keyword in lower-case) so identifiers
/// like <c>SELECT</c> / <c>select</c> / <c>Select</c> all classify as keywords.
/// Single-quoted string literals use the doubled-quote escape (<c>''</c>) per
/// the standard; backslash escapes are not recognized.
/// </remarks>
public static class SqlLexer
{
    /// <summary>General-keyword set (case-insensitive — entries are lowercase).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "select from where group having order by limit offset fetch next rows only join inner outer left right full cross on using union intersect except all distinct"u8,
        "as case when then else end if in is exists between like ilike and or not asc desc values into set returning with recursive begin commit rollback transaction grant revoke to for of"u8);

    /// <summary>Built-in SQL data type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "int integer smallint bigint tinyint decimal numeric float double real char varchar nvarchar nchar text ntext blob clob"u8,
        "date time timestamp datetime datetime2 interval bit boolean binary varbinary uuid json jsonb"u8);

    /// <summary>Declaration / DDL keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "create alter drop truncate table view index schema database trigger procedure function sequence type primary foreign"u8,
        "key constraint unique check references default auto_increment identity insert update delete merge declare cursor open close"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "true false null unknown"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "<> != <= >= || + - * / % = < >"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(),;."u8);

    /// <summary>First-byte set for the special-string rule (single-quoted SQL string).</summary>
    private static readonly SearchValues<byte> SqlStringFirst = SearchValues.Create("'"u8);

    /// <summary>First-byte set for the line-comment rule (<c>--</c>).</summary>
    private static readonly SearchValues<byte> DashFirst = SearchValues.Create("-"u8);

    /// <summary>Gets the singleton SQL lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the SQL lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        // Special string: single-quoted with doubled-quote escape (SQL standard).
        var sqlString = new LexerRule(
            TokenMatchers.MatchSingleQuotedDoubledEscape,
            TokenClass.StringSingle,
            LexerRule.NoStateChange) { FirstBytes = SqlStringFirst };

        CFamilyConfig config = new()
        {
            Tables = new()
            {
                Keywords = Keywords,
                KeywordTypes = KeywordTypes,
                KeywordDeclarations = KeywordDeclarations,
                KeywordConstants = KeywordConstants,
                Operators = OperatorTable
            },
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = sqlString
        };

        var coreRules = CFamilyRules.Build(config);

        // Add the SQL line-comment rule (-- to end of line) at the front.
        var lineComment = new LexerRule(
            static slice => TokenMatchers.MatchLineCommentToEol(slice, (byte)'-', (byte)'-'),
            TokenClass.CommentSingle,
            LexerRule.NoStateChange) { FirstBytes = DashFirst };

        var allRules = new LexerRule[coreRules.Length + 1];
        allRules[0] = lineComment;
        Array.Copy(coreRules, 0, allRules, 1, coreRules.Length);

        return new(LanguageRuleBuilder.BuildSingleState(allRules));
    }
}
