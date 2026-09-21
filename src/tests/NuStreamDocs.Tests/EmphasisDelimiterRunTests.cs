// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Emphasis pairing of mismatched and unmatched delimiter runs: the nearest opener wins, runs of different lengths pair, and the multiple-of-three rule applies.</summary>
public class EmphasisDelimiterRunTests
{
    /// <summary>Number of nested strong spans a long matched run may render before the remaining markers stay literal.</summary>
    private const int MaxNestedSpans = 32;

    /// <summary>Marker runs on either side of the content.</summary>
    private const int BothSides = 2;

    /// <summary>Marker bytes a strong span consumes, opener and closer together.</summary>
    private const int StrongMarkers = 4;

    /// <summary>Marker bytes an emphasis span consumes, opener and closer together.</summary>
    private const int EmphasisMarkers = 2;

    /// <summary>Number of repeated units in the long unpaired inputs.</summary>
    private const int LongRepeatCount = 3000;

    /// <summary>Upper bound on the rendered length of one <c>*a*</c> span with its separator.</summary>
    private const int SpanCapacity = 12;

    /// <summary>Gets runs whose closer takes only part of the opener, and whose leftover pairs again.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> MismatchedRunCases() =>
    [
        new(
            "*a **b* c** and _x __y_ z__ and ***a** b* and **a *b** c*",
            "<p><em>a <em><em>b</em> c</em></em> and <em>x <em><em>y</em> z</em></em> and <em><strong>a</strong> b</em> and <em><em>a <em>b</em></em> c</em></p>\n"),
        new("*a **b* c**", "<p><em>a <em><em>b</em> c</em></em></p>\n"),
        new("_x __y_ z__", "<p><em>x <em><em>y</em> z</em></em></p>\n"),
        new("*foo**bar**baz*", "<p><em>foo<strong>bar</strong>baz</em></p>\n"),
        new("*a**b***", "<p><em>a<strong>b</strong></em></p>\n"),
        new("**foo *bar** baz*", "<p><em><em>foo <em>bar</em></em> baz</em></p>\n"),
        new("*a **b **c** d** e*", "<p><em>a <strong>b <strong>c</strong> d</strong> e</em></p>\n"),
        new("__a _b _c_ d_ e__", "<p><strong>a <em>b <em>c</em> d</em> e</strong></p>\n"),
    ];

    /// <summary>Gets runs that the multiple-of-three rule keeps from pairing across lengths.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> MultipleOfThreeCases() =>
    [
        new("*foo**bar*", "<p><em>foo**bar</em></p>\n"),
        new("**foo*bar*", "<p>**foo<em>bar</em></p>\n"),
        new("**a*b**", "<p><strong>a*b</strong></p>\n"),
        new("(*.**", "<p>(*.**</p>\n"),
        new("****a****", "<p><strong><strong>a</strong></strong></p>\n"),
        new("*****a*****", "<p><em><strong><strong>a</strong></strong></em></p>\n"),
    ];

    /// <summary>Gets shapes that are matched the same way by every reading of the rules.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> StableShapeCases() =>
    [
        new("**a *b* c**", "<p><strong>a <em>b</em> c</strong></p>\n"),
        new("*a **b** c*", "<p><em>a <strong>b</strong> c</em></p>\n"),
        new("***a** b*", "<p><em><strong>a</strong> b</em></p>\n"),
        new("***a* b**", "<p><strong><em>a</em> b</strong></p>\n"),
        new("*a *b* c*", "<p><em>a <em>b</em> c</em></p>\n"),
        new("_a __b__ c_", "<p><em>a <strong>b</strong> c</em></p>\n"),
        new("*a*b*c*", "<p><em>a</em>b<em>c</em></p>\n"),
        new("**a**b**c**", "<p><strong>a</strong>b<strong>c</strong></p>\n"),
        new("*a*_b_", "<p><em>a</em><em>b</em></p>\n"),
        new("_a_*b*", "<p><em>a</em><em>b</em></p>\n"),
        new("*(*foo*)*", "<p><em>(<em>foo</em>)</em></p>\n"),
        new("**(**foo**)**", "<p><strong>(<strong>foo</strong>)</strong></p>\n"),
    ];

    /// <summary>Gets underscore runs inside words, which never open or close emphasis.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> IntrawordUnderscoreCases() =>
    [
        new("foo_bar_baz", "<p>foo_bar_baz</p>\n"),
        new("foo__bar__baz", "<p>foo__bar__baz</p>\n"),
        new("foo___bar___baz", "<p>foo___bar___baz</p>\n"),
        new("foo___bar___baz _x_ __y__", "<p>foo___bar___baz <em>x</em> <strong>y</strong></p>\n"),
        new("snake_case_word and __init__ and foo___bar___baz and _x_", "<p>snake_case_word and <strong>init</strong> and foo___bar___baz and <em>x</em></p>\n"),
        new("_a_b_c_", "<p><em>a_b_c</em></p>\n"),
        new("*a_b*", "<p><em>a_b</em></p>\n"),
        new("__a_b__", "<p><strong>a_b</strong></p>\n"),
        new("foo*bar*baz", "<p>foo<em>bar</em>baz</p>\n"),
    ];

    /// <summary>Gets markers hidden by escapes, code spans, links and raw HTML.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> HiddenMarkerCases() =>
    [
        new("\\*a*", "<p>*a*</p>\n"),
        new("\\_a_", "<p>_a_</p>\n"),
        new("*a\\*b*", "<p><em>a*b</em></p>\n"),
        new("_a\\__", "<p><em>a_</em></p>\n"),
        new("**a\\**", "<p>*<em>a*</em></p>\n"),
        new("*a `*` b*", "<p><em>a <code>*</code> b</em></p>\n"),
        new("`*a*`", "<p><code>*a*</code></p>\n"),
        new("*a `x*` b", "<p>*a <code>x*</code> b</p>\n"),
        new("[*a*](u)", "<p><a href=\"u\"><em>a</em></a></p>\n"),
        new("[*a](u) b*", "<p><a href=\"u\">*a</a> b*</p>\n"),
        new("*[a](u)*", "<p><em><a href=\"u\">a</a></em></p>\n"),
        new("**[a](u)**", "<p><strong><a href=\"u\">a</a></strong></p>\n"),
        new("*a [b*](u) c*", "<p><em>a <a href=\"u\">b*</a> c</em></p>\n"),
        new("*[bar*](/url)", "<p>*<a href=\"/url\">bar*</a></p>\n"),
        new("_foo [bar_](/url)", "<p>_foo <a href=\"/url\">bar_</a></p>\n"),
        new("**a [b**](u)**", "<p><strong>a <a href=\"u\">b**</a></strong></p>\n"),
        new("<span title=\"_\">_a_</span>", "<p><span title=\"_\"><em>a</em></span></p>\n"),
        new("*a <!-- * --> b*", "<p><em>a <!-- * --> b</em></p>\n"),
    ];

    /// <summary>Renders every recorded case and compares it with the recorded HTML.</summary>
    /// <param name="testCase">Source text and expected HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [MethodDataSource(nameof(MismatchedRunCases))]
    [MethodDataSource(nameof(MultipleOfThreeCases))]
    [MethodDataSource(nameof(StableShapeCases))]
    [MethodDataSource(nameof(IntrawordUnderscoreCases))]
    [MethodDataSource(nameof(HiddenMarkerCases))]
    public async Task RendersRecordedOutput(MarkdownCase testCase) =>
        await Assert.That(Render(testCase.Markdown)).IsEqualTo(testCase.Expected);

    /// <summary>Paragraphs with more markers than fit the fixed-size delimiter table pair the same way as short ones.</summary>
    /// <param name="spans">Number of <c>*a*</c> spans in the paragraph.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(16)]
    [Arguments(17)]
    [Arguments(200)]
    public async Task ManyPairedSpansRender(int spans)
    {
        var expected = new StringBuilder("<p>", spans * SpanCapacity);
        for (var i = 0; i < spans; i++)
        {
            _ = expected.Append(i is 0 ? "<em>a</em>" : " <em>a</em>");
        }

        _ = expected.Append("</p>\n");
        await Assert.That(Render(Repeat("*a* ", spans))).IsEqualTo(expected.ToString());
    }

    /// <summary>Closers with no opener and openers with no closer stay literal at any length.</summary>
    /// <param name="unit">Repeated text, ending in a space.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("a* ")]
    [Arguments("a_ ")]
    [Arguments("a** ")]
    [Arguments("*a ")]
    [Arguments("__a ")]
    public async Task UnpairedRunsStayLiteral(string unit)
    {
        var markdown = Repeat(unit, LongRepeatCount);
        await Assert.That(Render(markdown)).IsEqualTo($"<p>{markdown.TrimEnd()}</p>\n");
    }

    /// <summary>Unpaired runs of one marker do not stop runs of the other marker from pairing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OtherMarkerPairsPastUnpairedRuns()
    {
        var run = Repeat("*a ", LongRepeatCount);
        await Assert.That(Render($"{run}_b_ __c__")).IsEqualTo($"<p>{run}<em>b</em> <strong>c</strong></p>\n");
    }

    /// <summary>A long matched run nests as many spans as the renderer allows and keeps the rest literal, with every tag balanced and no marker lost.</summary>
    /// <param name="markers">Marker count on each side of the content.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(2)]
    [Arguments(64)]
    [Arguments(200)]
    [Arguments(5000)]
    public async Task LongMatchedRunsNestWithBalancedTags(int markers)
    {
        var html = Render($"{new string('*', markers)}a{new string('*', markers)}");
        var strongOpened = Count(html, "<strong>");
        var strongClosed = Count(html, "</strong>");
        var emphasisOpened = Count(html, "<em>");
        var emphasisClosed = Count(html, "</em>");
        await Assert.That(strongOpened).IsEqualTo(strongClosed);
        await Assert.That(emphasisOpened).IsEqualTo(emphasisClosed);
        await Assert.That(strongOpened + emphasisOpened).IsLessThanOrEqualTo(MaxNestedSpans);
        var literalMarkers = (BothSides * markers) - (StrongMarkers * strongOpened) - (EmphasisMarkers * emphasisOpened);
        await Assert.That(Count(html, "*")).IsEqualTo(literalMarkers);
    }

    /// <summary>Renders <paramref name="markdown"/> to an HTML string.</summary>
    /// <param name="markdown">Markdown text.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(Encoding.UTF8.GetBytes(markdown), writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    /// <summary>Concatenates <paramref name="count"/> copies of <paramref name="text"/>.</summary>
    /// <param name="text">Text to repeat.</param>
    /// <param name="count">Number of copies.</param>
    /// <returns>The repeated text.</returns>
    private static string Repeat(string text, int count)
    {
        StringBuilder builder = new(text.Length * count);
        for (var i = 0; i < count; i++)
        {
            _ = builder.Append(text);
        }

        return builder.ToString();
    }

    /// <summary>Counts the non-overlapping occurrences of <paramref name="value"/> in <paramref name="text"/>.</summary>
    /// <param name="text">Text to search.</param>
    /// <param name="value">Text to count.</param>
    /// <returns>The number of occurrences.</returns>
    private static int Count(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
