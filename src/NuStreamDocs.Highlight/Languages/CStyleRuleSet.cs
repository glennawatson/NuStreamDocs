// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Declarative rule set for C-style single-state lexers.</summary>
/// <remarks>
/// This runs only at lexer construction time, so the abstraction helps us
/// share the ordered rule-array shape across languages without affecting the
/// hot tokenisation path.
/// </remarks>
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
