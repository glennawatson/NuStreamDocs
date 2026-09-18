// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Declarative rule set for C-style single-state lexers.</summary>
/// <param name="Whitespace">Matches whitespace.</param>
/// <param name="DocComment">Matches documentation comments when supported.</param>
/// <param name="LineComment">Matches single-line comments.</param>
/// <param name="BlockComment">Matches block comments.</param>
/// <param name="Preprocessor">Matches preprocessor directives when supported.</param>
/// <param name="SpecialString">Matches language-specific strings when supported.</param>
/// <param name="DoubleString">Matches double-quoted strings.</param>
/// <param name="SingleString">Matches single-quoted strings.</param>
/// <param name="CharacterLiteral">Matches character literals when supported.</param>
/// <param name="HexNumber">Matches hexadecimal numbers.</param>
/// <param name="FloatNumber">Matches floating-point numbers.</param>
/// <param name="IntegerNumber">Matches integer numbers.</param>
/// <param name="KeywordConstant">Matches constant keywords.</param>
/// <param name="KeywordType">Matches type keywords.</param>
/// <param name="KeywordDeclaration">Matches declaration keywords.</param>
/// <param name="Keyword">Matches general keywords.</param>
/// <param name="Identifier">Matches identifiers.</param>
/// <param name="Operator">Matches operators.</param>
/// <param name="Punctuation">Matches punctuation.</param>
internal readonly record struct CStyleRuleSet(
    LexerRule Whitespace,
    LexerRule? DocComment,
    LexerRule LineComment,
    LexerRule BlockComment,
    LexerRule? Preprocessor,
    LexerRule? SpecialString,
    LexerRule DoubleString,
    LexerRule SingleString,
    LexerRule? CharacterLiteral,
    LexerRule HexNumber,
    LexerRule FloatNumber,
    LexerRule IntegerNumber,
    LexerRule KeywordConstant,
    LexerRule KeywordType,
    LexerRule KeywordDeclaration,
    LexerRule Keyword,
    LexerRule Identifier,
    LexerRule Operator,
    LexerRule Punctuation);
