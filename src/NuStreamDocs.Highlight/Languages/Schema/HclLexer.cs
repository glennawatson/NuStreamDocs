// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Schema;

/// <summary>HashiCorp Configuration Language (HCL) / Terraform lexer.</summary>
/// <remarks>
/// Block-style configuration: <c>resource "foo" "bar" { … }</c>. Recognizes
/// <c>#</c> and <c>//</c> line comments, <c>/* */</c> block comments, the
/// resource-block declaration keywords (<c>resource</c>, <c>variable</c>,
/// <c>data</c>, <c>module</c>, <c>output</c>, <c>locals</c>, <c>provider</c>,
/// <c>terraform</c>), and the standard HCL operators. <c>${...}</c>
/// interpolation expressions stay inside the surrounding string token.
/// </remarks>
public static class HclLexer
{
    /// <summary>General-keyword set (<c>for</c>, <c>in</c>, …).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "for in if else endfor endif"u8);

    /// <summary>Built-in primitive type keywords (Terraform 0.12+ type constraints).</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "string number bool any list map set object tuple"u8);

    /// <summary>Block-declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "resource data variable output locals module provider terraform backend required_providers required_version dynamic lifecycle depends_on"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("true false null"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("=!<>&|+-*/%?:"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "... == != <= >= && || => -> + - * / % = < > ! ?"u8);

    /// <summary>Gets the singleton HCL lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the HCL lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        SchemaFamilyConfig config = new()
        {
            IncludeHashComment = true,
            IncludeSlashComments = true,
            IncludeTripleQuotedString = false,
            IncludeSingleQuotedString = false,
            SigilFirst = null,
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst,
            Punctuation = PunctuationSet
        };

        return SchemaFamilyRules.CreateLexer(config);
    }
}
