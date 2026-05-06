// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateIgnoreCase(
        [.. "select"u8],
        [.. "from"u8],
        [.. "where"u8],
        [.. "group"u8],
        [.. "having"u8],
        [.. "order"u8],
        [.. "by"u8],
        [.. "limit"u8],
        [.. "offset"u8],
        [.. "fetch"u8],
        [.. "next"u8],
        [.. "rows"u8],
        [.. "only"u8],
        [.. "join"u8],
        [.. "inner"u8],
        [.. "outer"u8],
        [.. "left"u8],
        [.. "right"u8],
        [.. "full"u8],
        [.. "cross"u8],
        [.. "on"u8],
        [.. "using"u8],
        [.. "union"u8],
        [.. "intersect"u8],
        [.. "except"u8],
        [.. "all"u8],
        [.. "distinct"u8],
        [.. "as"u8],
        [.. "case"u8],
        [.. "when"u8],
        [.. "then"u8],
        [.. "else"u8],
        [.. "end"u8],
        [.. "if"u8],
        [.. "in"u8],
        [.. "is"u8],
        [.. "exists"u8],
        [.. "between"u8],
        [.. "like"u8],
        [.. "ilike"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "asc"u8],
        [.. "desc"u8],
        [.. "values"u8],
        [.. "into"u8],
        [.. "set"u8],
        [.. "returning"u8],
        [.. "with"u8],
        [.. "recursive"u8],
        [.. "begin"u8],
        [.. "commit"u8],
        [.. "rollback"u8],
        [.. "transaction"u8],
        [.. "grant"u8],
        [.. "revoke"u8],
        [.. "to"u8],
        [.. "for"u8],
        [.. "of"u8]);

    /// <summary>Built-in SQL data type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateIgnoreCase(
        [.. "int"u8],
        [.. "integer"u8],
        [.. "smallint"u8],
        [.. "bigint"u8],
        [.. "tinyint"u8],
        [.. "decimal"u8],
        [.. "numeric"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "real"u8],
        [.. "char"u8],
        [.. "varchar"u8],
        [.. "nvarchar"u8],
        [.. "nchar"u8],
        [.. "text"u8],
        [.. "ntext"u8],
        [.. "blob"u8],
        [.. "clob"u8],
        [.. "date"u8],
        [.. "time"u8],
        [.. "timestamp"u8],
        [.. "datetime"u8],
        [.. "datetime2"u8],
        [.. "interval"u8],
        [.. "bit"u8],
        [.. "boolean"u8],
        [.. "binary"u8],
        [.. "varbinary"u8],
        [.. "uuid"u8],
        [.. "json"u8],
        [.. "jsonb"u8]);

    /// <summary>Declaration / DDL keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateIgnoreCase(
        [.. "create"u8],
        [.. "alter"u8],
        [.. "drop"u8],
        [.. "truncate"u8],
        [.. "table"u8],
        [.. "view"u8],
        [.. "index"u8],
        [.. "schema"u8],
        [.. "database"u8],
        [.. "trigger"u8],
        [.. "procedure"u8],
        [.. "function"u8],
        [.. "sequence"u8],
        [.. "type"u8],
        [.. "primary"u8],
        [.. "foreign"u8],
        [.. "key"u8],
        [.. "constraint"u8],
        [.. "unique"u8],
        [.. "check"u8],
        [.. "references"u8],
        [.. "default"u8],
        [.. "auto_increment"u8],
        [.. "identity"u8],
        [.. "insert"u8],
        [.. "update"u8],
        [.. "delete"u8],
        [.. "merge"u8],
        [.. "declare"u8],
        [.. "cursor"u8],
        [.. "open"u8],
        [.. "close"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateIgnoreCase(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8],
        [.. "unknown"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<>"u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "||"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8]
    ];

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!|"u8);

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
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst,
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
