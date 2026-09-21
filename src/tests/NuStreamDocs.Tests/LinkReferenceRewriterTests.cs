// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Tests;

/// <summary>Tests reference-link label normalization.</summary>
public sealed class LinkReferenceRewriterTests
{
    /// <summary>Label matching preserves case folding across scratch-buffer sizes.</summary>
    /// <param name="length">Label length in bytes.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(255)]
    [Arguments(256)]
    [Arguments(257)]
    [Arguments(4096)]
    public async Task Rewrite_NormalizesLabelsAcrossBufferSizes(int length)
    {
        var reference = new string('A', length);
        var definition = new string('a', length);
        var source = Encoding.UTF8.GetBytes($"[text][{reference}]\n[{definition}]: /guide\n");

        var result = LinkReferenceRewriter.Rewrite(source);

        await Assert.That(Encoding.UTF8.GetString(result)).IsEqualTo("[text](/guide)\n");
    }

    /// <summary>Labels match regardless of ASCII case, whitespace run length, and whitespace kind.</summary>
    /// <param name="reference">Label used at the reference site.</param>
    /// <param name="definition">Label used at the definition.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("Foo Bar", "foo bar")]
    [Arguments("foo   bar", "FOO BAR")]
    [Arguments("foo\tbar", "foo bar")]
    [Arguments("foo bar", "  foo \t bar  ")]
    [Arguments("Ünï", "Ünï")]
    [Arguments("Ünï  Çode", "Ünï Çode")]
    public async Task Rewrite_MatchesEquivalentLabels(string reference, string definition)
    {
        var source = Encoding.UTF8.GetBytes($"[text][{reference}]\n\n[{definition}]: /guide\n");

        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(source));

        await Assert.That(result).IsEqualTo("[text](/guide)\n\n");
    }

    /// <summary>Case folding covers ASCII only, so labels that differ in non-ASCII case stay unresolved.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Rewrite_DoesNotFoldNonAsciiCase()
    {
        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite("[text][Ünï]\n\n[ünï]: /guide\n"u8));

        await Assert.That(result).IsEqualTo("[text][Ünï]\n\n");
    }

    /// <summary>A label that differs by more than case and whitespace stays unresolved.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Rewrite_LeavesUnmatchedLabelUnresolved()
    {
        var source = "[text][foobar]\n\n[foo bar]: /guide\n"u8;

        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(source));

        await Assert.That(result).IsEqualTo("[text][foobar]\n\n");
    }

    /// <summary>The first definition of a label wins.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Rewrite_FirstDefinitionWins()
    {
        var source = "[text][a]\n\n[A]: /first\n[a]: /second\n"u8;

        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(source));

        await Assert.That(result).IsEqualTo("[text](/first)\n\n");
    }

    /// <summary>Definitions with and without titles emit the title only when present.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Rewrite_EmitsTitleOnlyWhenPresent()
    {
        var source = "[a][x] [b][y] [c][z]\n\n[x]: /one \"T1\"\n[y]: /two\n[z]: /three ()\n"u8;

        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(source));

        await Assert.That(result).IsEqualTo("[a](/one \"T1\") [b](/two) [c](/three)\n\n");
    }

    /// <summary>Many definitions resolve correctly when they outgrow the initial table size.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Rewrite_ResolvesManyDefinitions()
    {
        const int count = 300;
        StringBuilder markdown = new();
        StringBuilder expected = new();
        for (var i = 0; i < count; i++)
        {
            _ = markdown.Append("[t][L").Append(i).Append("] ");
            _ = expected.Append("[t](/u").Append(i).Append(") ");
        }

        _ = markdown.Append("\n\n");
        _ = expected.Append("\n\n");
        for (var i = 0; i < count; i++)
        {
            _ = markdown.Append("[l").Append(i).Append("]: /u").Append(i).Append('\n');
        }

        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(Encoding.UTF8.GetBytes(markdown.ToString())));

        await Assert.That(result).IsEqualTo(expected.ToString());
    }

    /// <summary>
    /// A reference link that sits inside, or holds, another link is written with a space before its href; every other reference link is not.
    /// References inside the label of a reference link are resolved.
    /// </summary>
    /// <param name="source">Markdown source.</param>
    /// <param name="expected">Expected rewritten source.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("[a [l][r]](u)\n\n[r]: /r\n", "[a [l]( /r)](u)\n\n")]
    [Arguments("[a [r] b](u)\n\n[r]: /r\n", "[a [r]( /r) b](u)\n\n")]
    [Arguments("[a [r][] b](u)\n\n[r]: /r \"t\"\n", "[a [r]( /r \"t\") b](u)\n\n")]
    [Arguments("[a [l](m) b][r]\n\n[r]: /r\n", "[a [l](m) b]( /r)\n\n")]
    [Arguments("[a [k](j) [l][r]](u)\n\n[r]: /r\n", "[a [k](j) [l]( /r)](u)\n\n")]
    [Arguments("[a](b) [l][r]\n\n[r]: /r\n", "[a](b) [l](/r)\n\n")]
    [Arguments("[l][r] then [a](b)\n\n[r]: /r\n", "[l](/r) then [a](b)\n\n")]
    [Arguments("[a [r] b][s]\n\n[r]: /r\n[s]: /s\n", "[a [r]( /r) b]( /s)\n\n", DisplayName = "shortcut reference in full reference")]
    [Arguments("[a [r] b][]\n\n[a [r] b]: /s\n[r]: /r\n", "[a [r]( /r) b]( /s)\n\n", DisplayName = "shortcut reference in collapsed reference")]
    [Arguments("[a [r] b]\n\n[a [r] b]: /s\n[r]: /r\n", "[a [r]( /r) b]( /s)\n\n", DisplayName = "shortcut reference in shortcut reference")]
    [Arguments("[a [r][] b][s]\n\n[r]: /r\n[s]: /s\n", "[a [r]( /r) b]( /s)\n\n", DisplayName = "collapsed reference in full reference")]
    [Arguments("[a [l][r] b][s]\n\n[r]: /r\n[s]: /s\n", "[a [l]( /r) b]( /s)\n\n", DisplayName = "full reference in full reference")]
    [Arguments("[[r]][s]\n\n[r]: /r\n[s]: /s\n", "[[r]( /r)]( /s)\n\n", DisplayName = "label made of one reference")]
    [Arguments("[a [r] b [q] c][s]\n\n[r]: /r\n[q]: /q\n[s]: /s\n", "[a [r]( /r) b [q]( /q) c]( /s)\n\n", DisplayName = "two references")]
    [Arguments("[a [r] [l](m) b][s]\n\n[r]: /r\n[s]: /s\n", "[a [r]( /r) [l](m) b]( /s)\n\n", DisplayName = "reference next to inline link")]
    [Arguments("[a [b [r] c] d][s]\n\n[b [r] c]: /b\n[r]: /r\n[s]: /s\n", "[a [b [r]( /r) c]( /b) d]( /s)\n\n", DisplayName = "reference in reference in reference")]
    [Arguments("[a [r] b][s]\n\n[r]: /r \"t\"\n[s]: /s \"u\"\n", "[a [r]( /r \"t\") b]( /s \"u\")\n\n", DisplayName = "titles")]
    [Arguments("[a [u] b][s]\n\n[s]: /s\n", "[a [u] b](/s)\n\n", DisplayName = "undefined inner reference")]
    [Arguments("[a `[r]` b][s]\n\n[r]: /r\n[s]: /s\n", "[a `[r]` b](/s)\n\n", DisplayName = "inner reference in code span")]
    [Arguments("[a [r] b][u]\n\n[r]: /r\n", "[a [r](/r) b][u]\n\n", DisplayName = "undefined outer reference")]
    [Arguments("[a [r] b][s]\n\n[r]: /r\n[s]: /s\n\n[r] then [a] [r]\n", "[a [r]( /r) b]( /s)\n\n\n[r](/r) then [a] [r](/r)\n", DisplayName = "references after the link stay plain")]
    [Arguments("![a [r] b][s]\n\n[r]: /r\n[s]: /s\n", "![a [r] b](/s)\n\n", DisplayName = "image alt text stays as written")]
    public async Task Rewrite_MarksReferenceLinksThatNestWithInlineLinks(string source, string expected)
    {
        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(Encoding.UTF8.GetBytes(source)));

        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>Reference labels nested past the depth limit are copied as written.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Rewrite_StopsResolvingLabelsPastTheDepthLimit()
    {
        const int levels = 40;
        const int resolvedLevels = 33;
        var text = new string[levels + 1];
        text[0] = "x";
        for (var i = 1; i <= levels; i++)
        {
            text[i] = $"[{text[i - 1]}]";
        }

        StringBuilder markdown = new();
        _ = markdown.Append(text[levels]).Append("\n\n");
        for (var i = 0; i < levels; i++)
        {
            _ = markdown.Append('[').Append(text[i]).Append("]: /u").Append(i).Append('\n');
        }

        StringBuilder expected = new();
        for (var i = 1; i <= resolvedLevels; i++)
        {
            _ = expected.Append('[');
        }

        _ = expected.Append(text[levels - resolvedLevels]);
        for (var i = resolvedLevels; i >= 1; i--)
        {
            _ = expected.Append("]( /u").Append(levels - i).Append(')');
        }

        _ = expected.Append("\n\n");

        var result = Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(Encoding.UTF8.GetBytes(markdown.ToString())));

        await Assert.That(result).IsEqualTo(expected.ToString());
    }

    /// <summary>The writer overload produces the same output as the array overload.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Rewrite_WriterOverloadMatchesArrayOverload()
    {
        var source = "[text][a]\n\n[a]: /guide \"Title\"\n"u8.ToArray();
        System.Buffers.ArrayBufferWriter<byte> writer = new();

        LinkReferenceRewriter.Rewrite(source, writer);

        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo(Encoding.UTF8.GetString(LinkReferenceRewriter.Rewrite(source)));
    }
}
