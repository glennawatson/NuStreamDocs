// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;
using static System.Text.Encoding;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Theory-style tests for the cross-language matchers in <see cref="LanguageCommon"/>.</summary>
public class LanguageCommonTests
{
    /// <summary>C-style line comment <c>//</c> consumes to the end of the line.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("// trailing", 11)]
    [Arguments("// to eol\nrest", 9)]
    [Arguments("//\nrest", 2)]
    [Arguments("/ not a comment", 0)]
    [Arguments("/* block */", 0)]
    public async Task LineComment_consumes_to_eol(string input, int expected) =>
        await Assert.That(LanguageCommon.LineComment(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>C-style block comment <c>/* ... */</c> matches non-greedy through the first <c>*/</c>.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("/* hi */", 8)]
    [Arguments("/**//rest", 4)]
    [Arguments("/* a */ /* b */", 7)]
    [Arguments("/* unterminated", 0)]
    [Arguments("// not a block", 0)]
    [Arguments("/", 0)]
    public async Task BlockComment_consumes_to_close_marker(string input, int expected) =>
        await Assert.That(LanguageCommon.BlockComment(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Double-quoted no-escape strings consume through the closing quote without backslash handling.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("\"plain\"", 7)]
    [Arguments("\"\"", 2)]
    [Arguments("\"unterminated", 0)]
    [Arguments("'wrong quote'", 0)]
    public async Task DoubleQuotedStringNoEscape_handles_closing_quote(string input, int expected) =>
        await Assert.That(LanguageCommon.DoubleQuotedStringNoEscape(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Open-angle-slash (<c>&lt;/</c>) — the XML closing-tag introducer.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("</tag>", 2)]
    [Arguments("< /tag>", 0)]
    [Arguments("<tag>", 0)]
    [Arguments("<", 0)]
    public async Task AngleOpenSlash_matches_tag_close_introducer(string input, int expected) =>
        await Assert.That(LanguageCommon.AngleOpenSlash(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Self-closing-tag terminator (<c>/&gt;</c>).</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("/>", 2)]
    [Arguments("/ >", 0)]
    [Arguments("/", 0)]
    public async Task SelfClose_matches_self_closing_terminator(string input, int expected) =>
        await Assert.That(LanguageCommon.SelfClose(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Entity reference matches <c>&amp;name;</c> with letters / digits / <c>#</c>.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("&amp;", 5)]
    [Arguments("&#1234;", 7)]
    [Arguments("&copy;", 6)]
    [Arguments("&;", 0)]
    [Arguments("& a", 0)]
    [Arguments("&unterminated", 0)]
    public async Task EntityReference_matches_named_and_numeric(string input, int expected) =>
        await Assert.That(LanguageCommon.EntityReference(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Attribute name matches an identifier-shape followed by optional whitespace and a lookahead <c>=</c> (without consuming it).</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("href=\"x\"", 4)]
    [Arguments("data-id =\"x\"", 7)]
    [Arguments("xmlns:foo=\"x\"", 9)]
    [Arguments("name", 0)]
    [Arguments("=value", 0)]
    public async Task AttributeName_requires_equals_lookahead(string input, int expected) =>
        await Assert.That(LanguageCommon.AttributeName(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Tag name accepts letters / digits / underscore / colon / dot / dash, starting with a letter or underscore.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("div", 3)]
    [Arguments("svg:path", 8)]
    [Arguments("data-attr", 9)]
    [Arguments("a.b.c", 5)]
    [Arguments("123", 0)]
    [Arguments("-bad", 0)]
    public async Task TagName_matches_xml_name_grammar(string input, int expected) =>
        await Assert.That(LanguageCommon.TagName(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Triple-slash <c>///</c> consumes to end of line; anything else returns zero.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("/// xml-doc\nrest", 11)]
    [Arguments("/// trailing only", 17)]
    [Arguments("///\nrest", 3)]
    [Arguments("// regular", 0)]
    [Arguments("/ not a comment", 0)]
    [Arguments("", 0)]
    public async Task XmlDocCommentToEol_consumes_after_triple_slash(string input, int expected) =>
        await Assert.That(LanguageCommon.XmlDocCommentToEol(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>Single-character literal returns 3 for <c>'x'</c> and 4 for <c>'\x'</c>; everything else is zero.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("'a'", 3)]
    [Arguments("'\\n'", 4)]
    [Arguments("'\\t'rest", 4)]
    [Arguments("'ab'", 0)]
    [Arguments("not a char", 0)]
    [Arguments("'", 0)]
    [Arguments("''", 0)]
    public async Task CharLiteral_matches_typed_or_escape_form(string input, int expected) =>
        await Assert.That(LanguageCommon.CharLiteral(UTF8.GetBytes(input))).IsEqualTo(expected);

    /// <summary>The exposed length constants line up with the matcher results so consumers can size buffers without re-deriving them.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task LengthConstantsAlignWithMatcherResults()
    {
        // /// matches DocCommentPrefixLength bytes plus the rest-of-line length.
        await Assert.That(LanguageCommon.XmlDocCommentToEol("///"u8)).IsEqualTo(LanguageCommon.DocCommentPrefixLength);
        await Assert.That(LanguageCommon.CharLiteral("'a'"u8)).IsEqualTo(LanguageCommon.BasicCharLiteralLength);
        await Assert.That(LanguageCommon.CharLiteral("'\\n'"u8)).IsEqualTo(LanguageCommon.EscapedCharLiteralLength);
    }
}
