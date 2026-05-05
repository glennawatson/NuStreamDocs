// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using NuStreamDocs.Highlight;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Micro-benchmarks for the per-call helpers inside <c>NuStreamDocs.Highlight</c>.
/// </summary>
/// <remarks>
/// The end-to-end <c>HighlightBenchmarks</c> + <c>LanguageDetectorBenchmarks</c> measure the
/// composed pipeline; this fixture isolates the building blocks so regressions / wins land on a
/// single helper rather than on a full lexer pass. Covers the alias-lookup, keyword-set,
/// fence-attribute parser, and the most-used <c>TokenMatchers</c> primitives.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class HighlightHelperBenchmarks
{
    /// <summary>Representative C# keyword list used to populate the case-sensitive set fixture.</summary>
    private static readonly byte[][] CSharpKeywords =
    [
        [.. "abstract"u8], [.. "as"u8], [.. "base"u8], [.. "bool"u8], [.. "break"u8], [.. "byte"u8], [.. "case"u8], [.. "catch"u8], [.. "char"u8],
        [.. "checked"u8], [.. "class"u8], [.. "const"u8], [.. "continue"u8], [.. "decimal"u8], [.. "default"u8], [.. "delegate"u8],
        [.. "do"u8], [.. "double"u8], [.. "else"u8], [.. "enum"u8], [.. "event"u8], [.. "explicit"u8], [.. "extern"u8], [.. "false"u8],
        [.. "finally"u8], [.. "fixed"u8], [.. "float"u8], [.. "for"u8], [.. "foreach"u8], [.. "goto"u8], [.. "if"u8], [.. "implicit"u8],
        [.. "in"u8], [.. "int"u8], [.. "interface"u8], [.. "internal"u8], [.. "is"u8], [.. "lock"u8], [.. "long"u8], [.. "namespace"u8],
        [.. "new"u8], [.. "null"u8], [.. "object"u8], [.. "operator"u8], [.. "out"u8], [.. "override"u8], [.. "params"u8], [.. "private"u8],
        [.. "protected"u8], [.. "public"u8], [.. "readonly"u8], [.. "ref"u8], [.. "return"u8], [.. "sbyte"u8], [.. "sealed"u8],
        [.. "short"u8], [.. "sizeof"u8], [.. "stackalloc"u8], [.. "static"u8], [.. "string"u8], [.. "struct"u8], [.. "switch"u8],
        [.. "this"u8], [.. "throw"u8], [.. "true"u8], [.. "try"u8], [.. "typeof"u8], [.. "uint"u8], [.. "ulong"u8], [.. "unchecked"u8],
        [.. "unsafe"u8], [.. "ushort"u8], [.. "using"u8], [.. "virtual"u8], [.. "void"u8], [.. "volatile"u8], [.. "while"u8]
    ];

    /// <summary>Representative SQL keyword list used to populate the case-insensitive set fixture.</summary>
    private static readonly byte[][] SqlKeywordsLowercase =
    [
        [.. "select"u8], [.. "from"u8], [.. "where"u8], [.. "join"u8], [.. "inner"u8], [.. "outer"u8], [.. "left"u8], [.. "right"u8],
        [.. "group"u8], [.. "by"u8], [.. "order"u8], [.. "having"u8], [.. "insert"u8], [.. "update"u8], [.. "delete"u8], [.. "into"u8],
        [.. "values"u8], [.. "set"u8], [.. "as"u8], [.. "and"u8], [.. "or"u8], [.. "not"u8], [.. "null"u8], [.. "is"u8], [.. "in"u8], [.. "between"u8],
        [.. "like"u8], [.. "exists"u8], [.. "case"u8], [.. "when"u8], [.. "then"u8], [.. "else"u8], [.. "end"u8]
    ];

    /// <summary>Built-in registry; built once at <c>GlobalSetup</c>.</summary>
    private LexerRegistry _registry = null!;

    /// <summary>Hit alias — short, common (<c>cs</c>).</summary>
    private byte[] _aliasShortHit = [];

    /// <summary>Hit alias — medium length (<c>csharp</c>).</summary>
    private byte[] _aliasMediumHit = [];

    /// <summary>Hit alias — long (<c>typescript</c>).</summary>
    private byte[] _aliasLongHit = [];

    /// <summary>Miss alias — same length bucket as <see cref="_aliasMediumHit"/>, no match.</summary>
    private byte[] _aliasMiss = [];

    /// <summary>Case-sensitive keyword set with a representative C# keyword list.</summary>
    private ByteKeywordSet _keywordSet = null!;

    /// <summary>Case-insensitive keyword set used by SQL-/PowerShell-style lexers.</summary>
    private ByteKeywordSet _keywordSetIgnoreCase = null!;

    /// <summary>Hit word for the case-sensitive keyword set.</summary>
    private byte[] _keywordHit = [];

    /// <summary>Miss word — same length bucket as the hit, not in the set.</summary>
    private byte[] _keywordMiss = [];

    /// <summary>Hit word for the case-insensitive set, in mixed case.</summary>
    private byte[] _keywordHitMixedCase = [];

    /// <summary>Fence info-string with a single <c>title=</c> attribute.</summary>
    private byte[] _infoTitleOnly = [];

    /// <summary>Fence info-string with multiple attributes (title + linenums + hl_lines).</summary>
    private byte[] _infoMultiAttr = [];

    /// <summary>Fence info-string with no recognised attributes.</summary>
    private byte[] _infoBare = [];

    /// <summary>Identifier-only fixture (no whitespace, no operators) for <c>MatchAsciiIdentifier</c>.</summary>
    private byte[] _identifierFixture = [];

    /// <summary>Whitespace-only fixture for <c>MatchAsciiWhitespace</c>.</summary>
    private byte[] _whitespaceFixture = [];

    /// <summary>Line-comment fixture for <c>MatchLineCommentToEol</c>.</summary>
    private byte[] _lineCommentFixture = [];

    /// <summary>Double-quoted-string fixture for <c>MatchDoubleQuotedWithBackslashEscape</c>.</summary>
    private byte[] _doubleQuotedFixture = [];

    /// <summary>Hex-literal fixture for <c>MatchAsciiHexLiteral</c>.</summary>
    private byte[] _hexFixture = [];

    /// <summary>ASCII-digit run fixture for <c>MatchAsciiDigits</c>.</summary>
    private byte[] _digitsFixture = [];

    /// <summary>Unsigned-float fixture for <c>MatchUnsignedAsciiFloat</c>.</summary>
    private byte[] _floatFixture = [];

    /// <summary>Builds every fixture.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _registry = LexerRegistry.Default;

        _aliasShortHit = [.. "cs"u8];
        _aliasMediumHit = [.. "csharp"u8];
        _aliasLongHit = [.. "typescript"u8];
        _aliasMiss = [.. "tslang"u8]; // 6 bytes, same bucket as csharp; not registered

        _keywordSet = ByteKeywordSet.Create(CSharpKeywords);
        _keywordSetIgnoreCase = ByteKeywordSet.CreateIgnoreCase(SqlKeywordsLowercase);

        _keywordHit = [.. "namespace"u8];
        _keywordMiss = [.. "namespeic"u8]; // typo, same length as hit
        _keywordHitMixedCase = [.. "SELECT"u8];

        _infoTitleOnly = [.. "title=\"example.cs\""u8];
        _infoMultiAttr = [.. "title=\"prog.cs\" linenums=\"1\" hl_lines=\"3-5,7\""u8];
        _infoBare = [.. "highlight emphasised"u8];

        _identifierFixture = [.. "ReactiveCommandHelperViewModelExtended"u8];
        _whitespaceFixture = [.. "          \t\t\t  "u8];
        _lineCommentFixture = [.. "// this is a line comment running to the end of the line\nrest"u8];
        _doubleQuotedFixture = [.. "\"hello \\\"escaped\\\" world with various \\t \\n bytes inside\""u8];
        _hexFixture = [.. "0xCAFEBABE12345678U"u8];
        _digitsFixture = [.. "1234567890123"u8];
        _floatFixture = [.. "3.14159265358979"u8];
    }

    /// <summary>LexerRegistry alias hit — short alias bucket.</summary>
    /// <returns>True (hit).</returns>
    [Benchmark]
    public bool Registry_TryGet_ShortHit() =>
        _registry.TryGet(_aliasShortHit, out _);

    /// <summary>LexerRegistry alias hit — medium alias bucket.</summary>
    /// <returns>True (hit).</returns>
    [Benchmark]
    public bool Registry_TryGet_MediumHit() =>
        _registry.TryGet(_aliasMediumHit, out _);

    /// <summary>LexerRegistry alias hit — long alias bucket.</summary>
    /// <returns>True (hit).</returns>
    [Benchmark]
    public bool Registry_TryGet_LongHit() =>
        _registry.TryGet(_aliasLongHit, out _);

    /// <summary>LexerRegistry alias miss — same bucket as the medium hit, full bucket walk.</summary>
    /// <returns>False (miss).</returns>
    [Benchmark]
    public bool Registry_TryGet_Miss() =>
        _registry.TryGet(_aliasMiss, out _);

    /// <summary>ByteKeywordSet hit, case-sensitive.</summary>
    /// <returns>True.</returns>
    [Benchmark]
    public bool KeywordSet_Hit() =>
        _keywordSet.Contains(_keywordHit);

    /// <summary>ByteKeywordSet miss, case-sensitive.</summary>
    /// <returns>False.</returns>
    [Benchmark]
    public bool KeywordSet_Miss() =>
        _keywordSet.Contains(_keywordMiss);

    /// <summary>ByteKeywordSet case-insensitive hit on mixed-case input.</summary>
    /// <returns>True.</returns>
    [Benchmark]
    public bool KeywordSet_IgnoreCase_Hit() =>
        _keywordSetIgnoreCase.Contains(_keywordHitMixedCase);

    /// <summary>FenceAttrParser pulling <c>title="…"</c> from a single-attr fixture.</summary>
    /// <returns>True (attr present).</returns>
    [Benchmark]
    public bool FenceAttr_Title_FastHit() =>
        FenceAttrParser.TryGetTitle(_infoTitleOnly, out _);

    /// <summary>FenceAttrParser pulling <c>title="…"</c> from a multi-attr fixture (more bytes to scan).</summary>
    /// <returns>True (attr present).</returns>
    [Benchmark]
    public bool FenceAttr_Title_MultiAttr() =>
        FenceAttrParser.TryGetTitle(_infoMultiAttr, out _);

    /// <summary>FenceAttrParser miss — no <c>title=</c> attribute in the bare fixture.</summary>
    /// <returns>False.</returns>
    [Benchmark]
    public bool FenceAttr_Title_Miss() =>
        FenceAttrParser.TryGetTitle(_infoBare, out _);

    /// <summary>TokenMatchers identifier match against an all-identifier fixture.</summary>
    /// <returns>Identifier byte length.</returns>
    [Benchmark]
    public int TokenMatch_Identifier() =>
        TokenMatchers.MatchAsciiIdentifier(_identifierFixture);

    /// <summary>TokenMatchers ASCII whitespace match.</summary>
    /// <returns>Whitespace byte length.</returns>
    [Benchmark]
    public int TokenMatch_Whitespace() =>
        TokenMatchers.MatchAsciiWhitespace(_whitespaceFixture);

    /// <summary>TokenMatchers line-comment to EOL.</summary>
    /// <returns>Comment byte length.</returns>
    [Benchmark]
    public int TokenMatch_LineComment() =>
        TokenMatchers.MatchLineCommentToEol(_lineCommentFixture, (byte)'/', (byte)'/');

    /// <summary>TokenMatchers double-quoted string with backslash escape.</summary>
    /// <returns>String byte length.</returns>
    [Benchmark]
    public int TokenMatch_DoubleQuoted() =>
        TokenMatchers.MatchDoubleQuotedWithBackslashEscape(_doubleQuotedFixture);

    /// <summary>TokenMatchers hex literal (<c>0x…</c> with optional suffix).</summary>
    /// <returns>Hex literal byte length.</returns>
    [Benchmark]
    public int TokenMatch_HexLiteral() =>
        TokenMatchers.MatchAsciiHexLiteral(_hexFixture, TokenMatchers.AsciiHexDigits, TokenMatchers.AsciiIdentifierContinue);

    /// <summary>TokenMatchers unsigned float.</summary>
    /// <returns>Float byte length.</returns>
    [Benchmark]
    public int TokenMatch_UnsignedFloat() =>
        TokenMatchers.MatchUnsignedAsciiFloat(_floatFixture);

    /// <summary>TokenMatchers ASCII digit run.</summary>
    /// <returns>Digit byte length.</returns>
    [Benchmark]
    public int TokenMatch_Digits() =>
        TokenMatchers.MatchAsciiDigits(_digitsFixture);
}
