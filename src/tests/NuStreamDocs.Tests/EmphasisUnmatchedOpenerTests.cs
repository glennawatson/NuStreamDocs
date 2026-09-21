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
    /// <returns>Source text and expected HTML pairs.</returns>
    public static IEnumerable<(string Markdown, string Expected)> RunLengthCases()
    {
        yield return ("*a*", "<p><em>a</em></p>\n");
        yield return ("_a_", "<p><em>a</em></p>\n");
        yield return ("**a**", "<p><strong>a</strong></p>\n");
        yield return ("__a__", "<p><strong>a</strong></p>\n");
        yield return ("***a***", "<p><strong><em>a</em></strong></p>\n");
        yield return ("___a___", "<p><strong><em>a</em></strong></p>\n");
        yield return ("***a** b*", "<p><em><strong>a</strong> b</em></p>\n");
        yield return ("***a* b**", "<p><strong><em>a</em> b</strong></p>\n");
        yield return ("**a *b* c**", "<p><strong>a <em>b</em> c</strong></p>\n");
        yield return ("*a **b** c*", "<p><em>a <strong>b</strong> c</em></p>\n");
        yield return ("_a *b* c_", "<p><em>a <em>b</em> c</em></p>\n");
        yield return ("*a _b_ c*", "<p><em>a <em>b</em> c</em></p>\n");
        yield return ("**a*", "<p>*<em>a</em></p>\n");
        yield return ("*a**", "<p><em>a</em>*</p>\n");
        yield return ("**a*b*", "<p><em><em>a</em>b</em></p>\n");
        yield return ("*a **b*", "<p>*a *<em>b</em></p>\n");
        yield return ("***a*", "<p>**<em>a</em></p>\n");
        yield return ("***a**", "<p>*<strong>a</strong></p>\n");
        yield return ("*a***", "<p><em>a</em>**</p>\n");
        yield return ("**a***", "<p><strong>a</strong>*</p>\n");
        yield return ("****a****", "<p>*<strong><em>a</em></strong>*</p>\n");
        yield return ("*****a*****", "<p>**<strong><em>a</em></strong>**</p>\n");
    }

    /// <summary>Gets unclosed, misplaced and intra-word markers.</summary>
    /// <returns>Source text and expected HTML pairs.</returns>
    public static IEnumerable<(string Markdown, string Expected)> UnclosedAndIntraWordCases()
    {
        yield return ("**unclosed", "<p>**unclosed</p>\n");
        yield return ("*unclosed", "<p>*unclosed</p>\n");
        yield return ("unclosed**", "<p>unclosed**</p>\n");
        yield return ("a * b * c", "<p>a * b * c</p>\n");
        yield return ("a _ b _ c", "<p>a _ b _ c</p>\n");
        yield return ("* a *", "<ul>\n<li>a *</li>\n</ul>\n");
        yield return ("** a **", "<p>** a **</p>\n");
        yield return ("snake_case_word", "<p>snake_case_word</p>\n");
        yield return ("a_b_", "<p>a_b_</p>\n");
        yield return ("_a_b", "<p>_a_b</p>\n");
        yield return ("a_b_c_d", "<p>a_b_c_d</p>\n");
        yield return ("_a_b_", "<p><em>a_b</em></p>\n");
        yield return ("foo__bar__baz", "<p>foo__bar__baz</p>\n");
        yield return ("__init__", "<p><strong>init</strong></p>\n");
        yield return ("é_a_é", "<p>é_a_é</p>\n");
        yield return ("_é_", "<p><em>é</em></p>\n");
        yield return ("é*a*é", "<p>é<em>a</em>é</p>\n");
    }

    /// <summary>Gets code spans and escapes that hide markers.</summary>
    /// <returns>Source text and expected HTML pairs.</returns>
    public static IEnumerable<(string Markdown, string Expected)> CodeSpanAndEscapeCases()
    {
        yield return ("*a `*` b*", "<p><em>a <code>*</code> b</em></p>\n");
        yield return ("*a `x*` b", "<p>*a <code>x*</code> b</p>\n");
        yield return ("*a ``x`y*`` b*", "<p><em>a <code>x`y*</code> b</em></p>\n");
        yield return ("*a `unclosed*", "<p><em>a `unclosed</em></p>\n");
        yield return ("`*a*`", "<p><code>*a*</code></p>\n");
        yield return ("`a` *b*", "<p><code>a</code> <em>b</em></p>\n");
        yield return ("*`a`*", "<p><em><code>a</code></em></p>\n");
        yield return ("*a\\*b*", "<p><em>a*b</em></p>\n");
        yield return ("\\*a*", "<p>*a*</p>\n");
        yield return ("*a\\", "<p>*a\\</p>\n");
        yield return ("*a\\\\*", "<p><em>a\\</em></p>\n");
        yield return ("\\\\*a*", "<p>\\<em>a</em></p>\n");
        yield return ("*a \\` b*", "<p><em>a ` b</em></p>\n");
        yield return ("*a \\_ b*", "<p><em>a _ b</em></p>\n");
    }

    /// <summary>Gets nested and mixed-marker spans, lines, links and inline HTML.</summary>
    /// <returns>Source text and expected HTML pairs.</returns>
    public static IEnumerable<(string Markdown, string Expected)> NestedAndMixedCases()
    {
        yield return ("*a *b", "<p>*a *b</p>\n");
        yield return ("*a *b*", "<p>*a <em>b</em></p>\n");
        yield return ("*a *b* c", "<p>*a <em>b</em> c</p>\n");
        yield return ("*a **b** *c* d*", "<p><em>a <strong>b</strong> <em>c</em> d</em></p>\n");
        yield return ("*a *b *c* d* e*", "<p><em>a <em>b <em>c</em> d</em> e</em></p>\n");
        yield return ("**a *b **c** d* e**", "<p><strong>a <em>b <strong>c</strong> d</em> e</strong></p>\n");
        yield return ("*a _b* c_", "<p><em>a _b</em> c_</p>\n");
        yield return ("_a *b_ c*", "<p><em>a *b</em> c*</p>\n");
        yield return ("*a _b_ *c* d", "<p>*a <em>b</em> <em>c</em> d</p>\n");
        yield return ("*a ", "<p>*a</p>\n");
        yield return ("* a", "<ul>\n<li>a</li>\n</ul>\n");
        yield return ("a *", "<p>a *</p>\n");
        yield return ("*", "<ul>\n<li></li>\n</ul>\n");
        yield return ("**", "<p>**</p>\n");
        yield return ("***", "<hr />\n");
        yield return ("_", "<p>_</p>\n");
        yield return ("__", "<p>__</p>\n");
        yield return ("*_*", "<p><em>_</em></p>\n");
        yield return ("_*_", "<p><em>*</em></p>\n");
        yield return ("*_a*_", "<p><em>_a</em>_</p>\n");
        yield return ("_*a_*", "<p><em>*a</em>*</p>\n");
        yield return ("*a\nb*", "<p><em>a\nb</em></p>\n");
        yield return ("*a\n\nb*", "<p>*a</p>\n<p>b*</p>\n");
        yield return ("*a  \nb*", "<p><em>a<br />\nb</em></p>\n");
        yield return ("*a\\\nb*", "<p><em>a\\\nb</em></p>\n");
        yield return ("*a [b*](u) c*", "<p><em>a [b</em>](u) c*</p>\n");
        yield return ("[*a](u) b*", "<p><a href=\"u\">*a</a> b*</p>\n");
        yield return ("*a <b>c*</b> d*", "<p><em>a <b>c</em></b> d*</p>\n");
        yield return ("*a <b title=\"x*\">c</b>", "<p><em>a &lt;b title=&quot;x</em>&quot;&gt;c</b></p>\n");
        yield return ("*a `b*` [c*](u)", "<p><em>a <code>b*</code> [c</em>](u)</p>\n");
        yield return ("*a !b* [x](y)", "<p><em>a !b</em> <a href=\"y\">x</a></p>\n");
        yield return ("*a <http://x/*> b*", "<p><em>a &lt;http://x/</em>&gt; b*</p>\n");
        yield return ("<a href=\"*\">*a</a>*", "<p><a href=\"*\"><em>a</a></em></p>\n");
    }

    /// <summary>Gets repeated openers, adjacent spans and boundary input.</summary>
    /// <returns>Source text and expected HTML pairs.</returns>
    public static IEnumerable<(string Markdown, string Expected)> RepeatedAndBoundaryCases()
    {
        yield return ("*a *a *a *a", "<p>*a *a *a *a</p>\n");
        yield return ("*a *a *a *a*", "<p>*a *a *a <em>a</em></p>\n");
        yield return ("*a *a *a a*", "<p>*a *a <em>a a</em></p>\n");
        yield return ("**a **a **a", "<p>**a **a **a</p>\n");
        yield return ("**a **a a**", "<p>**a <strong>a a</strong></p>\n");
        yield return ("_a _a _a", "<p>_a _a _a</p>\n");
        yield return ("_a _a a_", "<p>_a <em>a a</em></p>\n");
        yield return ("*a _a *a _a", "<p>*a _a *a _a</p>\n");
        yield return ("*a\t*b*", "<p>*a\t<em>b</em></p>\n");
        yield return ("*a*b*c*", "<p><em>a</em>b<em>c</em></p>\n");
        yield return ("*a**b*", "<p><em>a</em><em>b</em></p>\n");
        yield return ("**a*b**", "<p><em><em>a</em>b</em>*</p>\n");
        yield return ("**a**b**", "<p><strong>a</strong>b**</p>\n");
        yield return ("*a*b*c*d*", "<p><em>a</em>b<em>c</em>d*</p>\n");
        yield return ("a*b*c", "<p>a<em>b</em>c</p>\n");
        yield return ("a**b**c", "<p>a<strong>b</strong>c</p>\n");
        yield return ("a***b***c", "<p>a<strong><em>b</em></strong>c</p>\n");
        yield return ("a_b_c", "<p>a_b_c</p>\n");
        yield return ("a__b__c", "<p>a__b__c</p>\n");
        yield return (
            "x *a* y **b** z ***c*** w _d_ v __e__ u ___f___",
            "<p>x <em>a</em> y <strong>b</strong> z <strong><em>c</em></strong> w <em>d</em> v <strong>e</strong> u <strong><em>f</em></strong></p>\n");
        yield return ("*a `b` c* `d *e* f` *g*", "<p><em>a <code>b</code> c</em> <code>d *e* f</code> <em>g</em></p>\n");
        yield return ("*a \\* b* \\*c\\* *d*", "<p><em>a * b</em> *c* <em>d</em></p>\n");
        yield return ("2 * 3 * 4", "<p>2 * 3 * 4</p>\n");
        yield return ("2*3*4", "<p>2<em>3</em>4</p>\n");
        yield return ("a * b*", "<p>a * b*</p>\n");
        yield return ("a *b * c*", "<p>a <em>b * c</em></p>\n");
        yield return ("**bold *and em** mismatch*", "<p><em><em>bold <em>and em</em></em> mismatch</em></p>\n");
    }

    /// <summary>Renders every recorded case and compares it with the recorded HTML.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [MethodDataSource(nameof(RunLengthCases))]
    [MethodDataSource(nameof(UnclosedAndIntraWordCases))]
    [MethodDataSource(nameof(CodeSpanAndEscapeCases))]
    [MethodDataSource(nameof(NestedAndMixedCases))]
    [MethodDataSource(nameof(RepeatedAndBoundaryCases))]
    public async Task RendersRecordedOutput(string markdown, string expected) =>
        await Assert.That(Render(markdown)).IsEqualTo(expected);

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
