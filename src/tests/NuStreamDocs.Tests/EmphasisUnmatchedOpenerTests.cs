// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Emphasis rendering across nested, mixed, escaped and unmatched marker runs, and the cost of long runs of unmatched openers.</summary>
public class EmphasisUnmatchedOpenerTests
{
    /// <summary>Number of unmatched openers in the bounded-time input.</summary>
    private const int UnmatchedOpenerCount = 2500;

    /// <summary>Renders timed per input, the fastest of which is compared with the bound.</summary>
    private const int TimedRuns = 5;

    /// <summary>Initial capacity of the reused output writer.</summary>
    private const int OutputCapacity = 32 * 1024;

    /// <summary>Upper bound for the fastest render of <see cref="UnmatchedOpenerCount"/> unmatched openers.</summary>
    private static readonly TimeSpan UnmatchedOpenerBound = TimeSpan.FromMilliseconds(10);

    /// <summary>Gets single, double and triple runs, with shorter or longer closing runs.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> RunLengthCases() =>
    [
        new("*a*", "<p><em>a</em></p>\n"),
        new("_a_", "<p><em>a</em></p>\n"),
        new("**a**", "<p><strong>a</strong></p>\n"),
        new("__a__", "<p><strong>a</strong></p>\n"),
        new("***a***", "<p><em><strong>a</strong></em></p>\n"),
        new("___a___", "<p><em><strong>a</strong></em></p>\n"),
        new("***a** b*", "<p><em><strong>a</strong> b</em></p>\n"),
        new("***a* b**", "<p><strong><em>a</em> b</strong></p>\n"),
        new("**a *b* c**", "<p><strong>a <em>b</em> c</strong></p>\n"),
        new("*a **b** c*", "<p><em>a <strong>b</strong> c</em></p>\n"),
        new("_a *b* c_", "<p><em>a <em>b</em> c</em></p>\n"),
        new("*a _b_ c*", "<p><em>a <em>b</em> c</em></p>\n"),
        new("**a*", "<p>*<em>a</em></p>\n"),
        new("*a**", "<p><em>a</em>*</p>\n"),
        new("**a*b*", "<p>**a<em>b</em></p>\n"),
        new("*a **b*", "<p>*a *<em>b</em></p>\n"),
        new("***a*", "<p>**<em>a</em></p>\n"),
        new("***a**", "<p>*<strong>a</strong></p>\n"),
        new("*a***", "<p><em>a</em>**</p>\n"),
        new("**a***", "<p><strong>a</strong>*</p>\n"),
        new("****a****", "<p><strong><strong>a</strong></strong></p>\n"),
        new("*****a*****", "<p><em><strong><strong>a</strong></strong></em></p>\n"),
    ];

    /// <summary>Gets unclosed, misplaced and intra-word markers.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> UnclosedAndIntraWordCases() =>
    [
        new("**unclosed", "<p>**unclosed</p>\n"),
        new("*unclosed", "<p>*unclosed</p>\n"),
        new("unclosed**", "<p>unclosed**</p>\n"),
        new("a * b * c", "<p>a * b * c</p>\n"),
        new("a _ b _ c", "<p>a _ b _ c</p>\n"),
        new("* a *", "<ul>\n<li>a *</li>\n</ul>\n"),
        new("** a **", "<p>** a **</p>\n"),
        new("snake_case_word", "<p>snake_case_word</p>\n"),
        new("a_b_", "<p>a_b_</p>\n"),
        new("_a_b", "<p>_a_b</p>\n"),
        new("a_b_c_d", "<p>a_b_c_d</p>\n"),
        new("_a_b_", "<p><em>a_b</em></p>\n"),
        new("foo__bar__baz", "<p>foo__bar__baz</p>\n"),
        new("__init__", "<p><strong>init</strong></p>\n"),
        new("é_a_é", "<p>é_a_é</p>\n"),
        new("_é_", "<p><em>é</em></p>\n"),
        new("é*a*é", "<p>é<em>a</em>é</p>\n"),
    ];

    /// <summary>Gets code spans and escapes that hide markers.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> CodeSpanAndEscapeCases() =>
    [
        new("*a `*` b*", "<p><em>a <code>*</code> b</em></p>\n"),
        new("*a `x*` b", "<p>*a <code>x*</code> b</p>\n"),
        new("*a ``x`y*`` b*", "<p><em>a <code>x`y*</code> b</em></p>\n"),
        new("*a `unclosed*", "<p><em>a `unclosed</em></p>\n"),
        new("`*a*`", "<p><code>*a*</code></p>\n"),
        new("`a` *b*", "<p><code>a</code> <em>b</em></p>\n"),
        new("*`a`*", "<p><em><code>a</code></em></p>\n"),
        new("*a\\*b*", "<p><em>a*b</em></p>\n"),
        new("\\*a*", "<p>*a*</p>\n"),
        new("*a\\", "<p>*a\\</p>\n"),
        new("*a\\\\*", "<p><em>a\\</em></p>\n"),
        new("\\\\*a*", "<p>\\<em>a</em></p>\n"),
        new("*a \\` b*", "<p><em>a ` b</em></p>\n"),
        new("*a \\_ b*", "<p><em>a _ b</em></p>\n"),
    ];

    /// <summary>Gets nested and mixed-marker spans, lines, links and inline HTML.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> NestedAndMixedCases() =>
    [
        new("*a *b", "<p>*a *b</p>\n"),
        new("*a *b*", "<p>*a <em>b</em></p>\n"),
        new("*a *b* c", "<p>*a <em>b</em> c</p>\n"),
        new("*a **b** *c* d*", "<p><em>a <strong>b</strong> <em>c</em> d</em></p>\n"),
        new("*a *b *c* d* e*", "<p><em>a <em>b <em>c</em> d</em> e</em></p>\n"),
        new("**a *b **c** d* e**", "<p><strong>a <em>b <strong>c</strong> d</em> e</strong></p>\n"),
        new("*a _b* c_", "<p><em>a _b</em> c_</p>\n"),
        new("_a *b_ c*", "<p><em>a *b</em> c*</p>\n"),
        new("*a _b_ *c* d", "<p>*a <em>b</em> <em>c</em> d</p>\n"),
        new("*a ", "<p>*a</p>\n"),
        new("* a", "<ul>\n<li>a</li>\n</ul>\n"),
        new("a *", "<p>a *</p>\n"),
        new("*", "<ul>\n<li></li>\n</ul>\n"),
        new("**", "<p>**</p>\n"),
        new("***", "<hr />\n"),
        new("_", "<p>_</p>\n"),
        new("__", "<p>__</p>\n"),
        new("*_*", "<p><em>_</em></p>\n"),
        new("_*_", "<p><em>*</em></p>\n"),
        new("*_a*_", "<p><em>_a</em>_</p>\n"),
        new("_*a_*", "<p><em>*a</em>*</p>\n"),
        new("*a\nb*", "<p><em>a\nb</em></p>\n"),
        new("*a\n\nb*", "<p>*a</p>\n<p>b*</p>\n"),
        new("*a  \nb*", "<p><em>a<br />\nb</em></p>\n"),
        new("*a\\\nb*", "<p><em>a\\\nb</em></p>\n"),
        new("*a [b*](u) c*", "<p><em>a <a href=\"u\">b*</a> c</em></p>\n"),
        new("[*a](u) b*", "<p><a href=\"u\">*a</a> b*</p>\n"),
        new("*a <b>c*</b> d*", "<p><em>a <b>c</em></b> d*</p>\n"),
        new("*a <b title=\"x*\">c</b>", "<p>*a <b title=\"x*\">c</b></p>\n"),
        new("*a `b*` [c*](u)", "<p>*a <code>b*</code> <a href=\"u\">c*</a></p>\n"),
        new("*a !b* [x](y)", "<p><em>a !b</em> <a href=\"y\">x</a></p>\n"),
        new("*a <http://x/*> b*", "<p><em>a <a href=\"http://x/*\">http://x/*</a> b</em></p>\n"),
        new("<a href=\"*\">*a</a>*", "<p><a href=\"*\"><em>a</a></em></p>\n"),
    ];

    /// <summary>Gets repeated openers, adjacent spans and boundary input.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> RepeatedAndBoundaryCases() =>
    [
        new("*a *a *a *a", "<p>*a *a *a *a</p>\n"),
        new("*a *a *a *a*", "<p>*a *a *a <em>a</em></p>\n"),
        new("*a *a *a a*", "<p>*a *a <em>a a</em></p>\n"),
        new("**a **a **a", "<p>**a **a **a</p>\n"),
        new("**a **a a**", "<p>**a <strong>a a</strong></p>\n"),
        new("_a _a _a", "<p>_a _a _a</p>\n"),
        new("_a _a a_", "<p>_a <em>a a</em></p>\n"),
        new("*a _a *a _a", "<p>*a _a *a _a</p>\n"),
        new("*a\t*b*", "<p>*a\t<em>b</em></p>\n"),
        new("*a*b*c*", "<p><em>a</em>b<em>c</em></p>\n"),
        new("*a**b*", "<p><em>a**b</em></p>\n"),
        new("**a*b**", "<p><strong>a*b</strong></p>\n"),
        new("**a**b**", "<p><strong>a</strong>b**</p>\n"),
        new("*a*b*c*d*", "<p><em>a</em>b<em>c</em>d*</p>\n"),
        new("a*b*c", "<p>a<em>b</em>c</p>\n"),
        new("a**b**c", "<p>a<strong>b</strong>c</p>\n"),
        new("a***b***c", "<p>a<em><strong>b</strong></em>c</p>\n"),
        new("a_b_c", "<p>a_b_c</p>\n"),
        new("a__b__c", "<p>a__b__c</p>\n"),
        new(
            "x *a* y **b** z ***c*** w _d_ v __e__ u ___f___",
            "<p>x <em>a</em> y <strong>b</strong> z <em><strong>c</strong></em> w <em>d</em> v <strong>e</strong> u <em><strong>f</strong></em></p>\n"),
        new("*a `b` c* `d *e* f` *g*", "<p><em>a <code>b</code> c</em> <code>d *e* f</code> <em>g</em></p>\n"),
        new("*a \\* b* \\*c\\* *d*", "<p><em>a * b</em> *c* <em>d</em></p>\n"),
        new("2 * 3 * 4", "<p>2 * 3 * 4</p>\n"),
        new("2*3*4", "<p>2<em>3</em>4</p>\n"),
        new("a * b*", "<p>a * b*</p>\n"),
        new("a *b * c*", "<p>a <em>b * c</em></p>\n"),
        new("**bold *and em** mismatch*", "<p><em><em>bold <em>and em</em></em> mismatch</em></p>\n"),
    ];

    /// <summary>Renders every recorded case and compares it with the recorded HTML.</summary>
    /// <param name="testCase">Source text and expected HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [MethodDataSource(nameof(RunLengthCases))]
    [MethodDataSource(nameof(UnclosedAndIntraWordCases))]
    [MethodDataSource(nameof(CodeSpanAndEscapeCases))]
    [MethodDataSource(nameof(NestedAndMixedCases))]
    [MethodDataSource(nameof(RepeatedAndBoundaryCases))]
    public async Task RendersRecordedOutput(MarkdownCase testCase) =>
        await Assert.That(Render(testCase.Markdown)).IsEqualTo(testCase.Expected);

    /// <summary>Long runs of one unmatched opener shape stay literal at any length.</summary>
    /// <param name="opener">Repeated opener text, ending in a space.</param>
    /// <param name="count">Repetitions.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("*a ", 1)]
    [Arguments("*a ", 300)]
    [Arguments("*a ", 1500)]
    [Arguments("*a ", UnmatchedOpenerCount)]
    [Arguments("_a ", UnmatchedOpenerCount)]
    [Arguments("**a ", UnmatchedOpenerCount)]
    [Arguments("***a ", UnmatchedOpenerCount)]
    [Arguments("*a ", 5000)]
    public async Task LongUnmatchedRunsStayLiteral(string opener, int count)
    {
        var markdown = Repeat(opener, count);
        await Assert.That(Render(markdown)).IsEqualTo($"<p>{markdown.TrimEnd()}</p>\n");
    }

    /// <summary>A closer that cannot pair with any earlier opener leaves a long unmatched run literal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnmatchedRunBeforeUnrelatedMarkerStaysLiteral()
    {
        var run = Repeat("*a ", UnmatchedOpenerCount);
        await Assert.That(Render($"{run}_b_")).IsEqualTo($"<p>{run}<em>b</em></p>\n");
        await Assert.That(Render($"{run}__b__")).IsEqualTo($"<p>{run}<strong>b</strong></p>\n");
    }

    /// <summary>Thousands of unmatched openers render within a fixed time bound.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnmatchedOpenersRenderWithinBound()
    {
        var bytes = Encoding.UTF8.GetBytes(Repeat("*a ", UnmatchedOpenerCount));
        ArrayBufferWriter<byte> writer = new(OutputCapacity);
        MarkdownRenderer.Render(bytes, writer);

        var fastest = TimeSpan.MaxValue;
        for (var run = 0; run < TimedRuns; run++)
        {
            writer.ResetWrittenCount();
            var started = Stopwatch.GetTimestamp();
            MarkdownRenderer.Render(bytes, writer);
            var elapsed = Stopwatch.GetElapsedTime(started);
            fastest = elapsed < fastest ? elapsed : fastest;
        }

        await Assert.That(fastest).IsLessThan(UnmatchedOpenerBound);
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

    /// <summary>Renders <paramref name="markdown"/> to an HTML string.</summary>
    /// <param name="markdown">Markdown text.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(Encoding.UTF8.GetBytes(markdown), writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
