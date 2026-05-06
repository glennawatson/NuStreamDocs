// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Scripting;

/// <summary>VB.NET lexer.</summary>
/// <remarks>
/// Case-insensitive C-family lexer driven by <see cref="CFamilyRules"/>; uses
/// <c>'</c> for line comments (handled as the single-string fallback because the
/// language has no character literals) and the standard <c>End</c>-block forms.
/// </remarks>
public static class VbNetLexer
{
    /// <summary>General-keyword set (case-insensitive — entries are lowercase).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "if then else elseif end select case for each next while do loop until return exit continue throw try catch finally"u8,
        "new me mybase myclass in to step as of is isnot with imports namespace yield await async handles addhandler removehandler raiseevent"u8,
        "and or not andalso orelse xor"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "boolean byte sbyte short ushort integer uinteger long ulong single double decimal char string object date datetime"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "dim const static shared readonly public private protected friend internal overrides overridable mustoverride notoverridable shadows overloads default"u8,
        "implements inherits interface class module structure enum delegate event property sub function operator byval byref optional paramarray partial mustinherit notinheritable"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "true false nothing"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "<= >= <> &= += -= *= /= + - * / & = < >"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){};,.:"u8);

    /// <summary>First-byte set for the special-string rule (single-quote line comment).</summary>
    private static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>Gets the singleton VB.NET lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the VB.NET lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        // VB.NET uses ' for line comments, not //. Wire it as the special-string slot.
        var lineComment = new LexerRule(
            static slice => TokenMatchers.MatchLineCommentToEol(slice, (byte)'\''),
            TokenClass.CommentSingle,
            LexerRule.NoStateChange) { FirstBytes = SingleQuoteFirst };

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
            SpecialString = lineComment
        };

        return CFamilyRules.CreateLexer(config);
    }
}
