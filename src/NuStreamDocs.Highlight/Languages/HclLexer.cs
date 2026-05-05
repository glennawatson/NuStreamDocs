// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "for"u8],
        [.. "in"u8],
        [.. "if"u8],
        [.. "else"u8],
        [.. "endfor"u8],
        [.. "endif"u8]);

    /// <summary>Built-in primitive type keywords (Terraform 0.12+ type constraints).</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "string"u8],
        [.. "number"u8],
        [.. "bool"u8],
        [.. "any"u8],
        [.. "list"u8],
        [.. "map"u8],
        [.. "set"u8],
        [.. "object"u8],
        [.. "tuple"u8]);

    /// <summary>Block-declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "resource"u8],
        [.. "data"u8],
        [.. "variable"u8],
        [.. "output"u8],
        [.. "locals"u8],
        [.. "module"u8],
        [.. "provider"u8],
        [.. "terraform"u8],
        [.. "backend"u8],
        [.. "required_providers"u8],
        [.. "required_version"u8],
        [.. "dynamic"u8],
        [.. "lifecycle"u8],
        [.. "depends_on"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("efi"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abnlmsot"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("rdvolmptbl"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("=!<>&|+-*/%?:"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "=>"u8],
        [.. "->"u8],
        [.. "..."u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "!"u8],
        [.. "?"u8]
    ];

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
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst,
            Punctuation = PunctuationSet
        };

        return new(LanguageRuleBuilder.BuildSingleState(SchemaFamilyRules.Build(config)));
    }
}
