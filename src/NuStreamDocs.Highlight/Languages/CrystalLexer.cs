// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Crystal lexer.</summary>
/// <remarks>
/// Brace-style language with character literals; uses <c>#</c> for line
/// comments (wired through the special-string slot since C-family normally
/// uses <c>//</c>) and the Ruby-derived <c>def</c>/<c>class</c>/<c>module</c>/<c>end</c>
/// declaration shape.
/// </remarks>
public static class CrystalLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus Crystal-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "end begin rescue ensure raise next redo retry yield self super require in as is_a? responds_to? as? and or not when then of with out"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Bool Char Int8 Int16 Int32 Int64 UInt8 UInt16 UInt32 UInt64 Float32 Float64 String Symbol Array Hash Tuple NamedTuple Set Nil"u8);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "def class struct module lib fun macro alias abstract private protected public enum type include extend annotation instance_sizeof sizeof"u8);

    /// <summary>Constant keywords — shared <c>true</c>/<c>false</c> plus Crystal's <c>nil</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false nil"u8);

    /// <summary>Operator alternation — shared C-style core plus Crystal's range / heredoc-style forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "... .. <=> === -> => ::"u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Single-byte structural punctuation — Crystal uses <c>@</c> for instance variables and <c>:</c> for symbols.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@:"u8);

    /// <summary>First-byte set for the special-string slot — used here to wire the <c>#</c> line-comment rule.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>Gets the singleton Crystal lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Crystal lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        // Crystal uses # for line comments; the C-family helper recognizes // by default,
        // so we slot the Crystal comment through the special-string position (which fires
        // ahead of strings).
        var hashComment = new LexerRule(
            TokenMatchers.MatchHashComment,
            TokenClass.CommentSingle,
            LexerRule.NoStateChange) { FirstBytes = HashFirst };

        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = hashComment
        };

        return CFamilyRules.CreateLexer(config);
    }
}
