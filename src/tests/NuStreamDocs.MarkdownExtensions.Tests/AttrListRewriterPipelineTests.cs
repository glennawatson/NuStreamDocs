// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using NuStreamDocs.MarkdownExtensions.AttrList;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Output-shape and buffer-reuse tests for the <c>AttrListRewriter</c> stage pipeline.</summary>
public class AttrListRewriterPipelineTests
{
    /// <summary>Repetitions that push a page well past the pooled-buffer size cap.</summary>
    private const int LargePageRepetitions = 30_000;

    /// <summary>Repetitions for the pages rewritten back to back on reused buffers.</summary>
    private const int ReusePageRepetitions = 5_000;

    /// <summary>Rounds of back-to-back rewrites in the buffer reuse test.</summary>
    private const int ReuseRounds = 3;

    /// <summary>Repetitions for the pages rewritten from concurrent workers.</summary>
    private const int ConcurrentPageRepetitions = 40;

    /// <summary>Concurrent workers started per page shape.</summary>
    private const int WorkersPerShape = 8;

    /// <summary>Total concurrent workers across both page shapes.</summary>
    private const int TotalWorkers = 16;

    /// <summary>Heading unit carrying an id and class attr-list.</summary>
    private const string HeadingUnit = "<h2>Section {: #s .lead }</h2>\n";

    /// <summary>Rewritten form of <see cref="HeadingUnit"/>.</summary>
    private const string HeadingUnitRewritten = "<h2 id=\"s\" class=\"lead\">Section</h2>\n";

    /// <summary>Paragraph unit whose attr-list holds HTML-escaped quotes.</summary>
    private const string EntityUnit = "<p>&quot;a&quot; {: title=&quot;t&quot; } &quot;b&quot;</p>\n";

    /// <summary>Rewritten form of <see cref="EntityUnit"/>.</summary>
    private const string EntityUnitRewritten = "<p title=\"t\">&quot;a&quot; &quot;b&quot;</p>\n";

    /// <summary>Paragraph unit with no attr-list syntax.</summary>
    private const string PlainUnit = "<p>nothing to see</p>\n";

    /// <summary>Page boundaries, malformed markers, block, inline and shorthand shapes produce the pinned output.</summary>
    /// <param name="html">Rendered page HTML.</param>
    /// <param name="expected">Expected rewritten HTML.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "")]
    [Arguments("<h1>Heading</h1><p>Body</p>", "<h1>Heading</h1><p>Body</p>")]
    [Arguments("{: #a }", "{: #a }")]
    [Arguments("{: #a } trailing", "{: #a } trailing")]
    [Arguments("<h1>Heading {: #intro .lead }</h1>", "<h1 id=\"intro\" class=\"lead\">Heading</h1>")]
    [Arguments("<h1>Heading {: #intro .lead }</h1><h2>Two {: #two }</h2><p>Para {: .x .y }</p>", "<h1 id=\"intro\" class=\"lead\">Heading</h1><h2 id=\"two\">Two</h2><p class=\"x y\">Para</p>")]
    [Arguments("{: .start }<p>x</p>", "{: .start }<p>x</p>")]
    [Arguments("<p>x</p>{: .end }", "<p>x</p>{: .end }")]
    [Arguments("<p>x {: .end }</p>", "<p class=\"end\">x</p>")]
    [Arguments("<p>x {: .unclosed</p>", "<p>x {: .unclosed</p>")]
    [Arguments("<p>x {: #a .b </p><h1>ok {: #z }</h1>", "<p>x {: #a .b </p><h1 id=\"z\">ok</h1>")]
    [Arguments("<p>x {:", "<p>x {:")]
    [Arguments("<p>x {", "<p>x {")]
    [Arguments("<p>{ }</p>", "<p></p>")]
    [Arguments("<p>{}</p>", "<p>{}</p>")]
    [Arguments("<p><a href=\"https://x.test\">here</a>{: target=\"_blank\" } for more.</p>", "<p><a href=\"https://x.test\" target=\"_blank\">here</a> for more.</p>")]
    [Arguments("<p><img src=\"/x.png\" alt=\"x\">{: .hero }</p>", "<p><img src=\"/x.png\" alt=\"x\" class=\"hero\"></p>")]
    [Arguments("<p><img src=\"/x.png\" />{: .hero }</p>", "<p><img src=\"/x.png\" class=\"hero\" /></p>")]
    [Arguments("<p><img src=\"/x.png\"   />{: .hero }</p>", "<p><img src=\"/x.png\" class=\"hero\"   /></p>")]
    [Arguments("<p class=\"existing\">Text {: .extra }</p>", "<p class=\"existing extra\">Text</p>")]
    [Arguments("<p id=\"old\" class=\"existing\">Text {: #new .extra k=v }</p>", "<p id=\"new\" class=\"existing extra\" k=\"v\">Text</p>")]
    [Arguments("<p><a href=\"/get-started/\">Get started</a>{ .md-button .md-button--primary }</p>", "<p><a href=\"/get-started/\" class=\"md-button md-button--primary\">Get started</a></p>")]
    [Arguments("<p><svg viewBox=\"0 0 24 24\"></svg>{ .lg .middle }</p>", "<p><svg viewBox=\"0 0 24 24\" class=\"lg middle\"></svg></p>")]
    [Arguments("<pre><code>var x = { foo: 1 };</code></pre>", "<pre><code>var x = { foo: 1 };</code></pre>")]
    [Arguments("<p><a href=\"\"></a>{#T:Foo.Bar}</p>", "<p><a href=\"\" id=\"T:Foo.Bar\"></a></p>")]
    [Arguments("<p><a href=\"\"></a>{#M:Foo.Bar.Baz(Foo.IThing)}</p>", "<p><a href=\"\" id=\"M:Foo.Bar.Baz(Foo.IThing)\"></a></p>")]
    [Arguments("<p><img src=\"x.png\" alt=\"x\">{ width=\"700\" }</p>", "<p><img src=\"x.png\" alt=\"x\" width=\"700\"></p>")]
    [Arguments("<p><img src=\"x.png\" alt=\"x\">{width=\"700\"}</p>", "<p><img src=\"x.png\" alt=\"x\" width=\"700\"></p>")]
    [Arguments("<p><img src=\"x.png\" alt=\"x\">{width=&quot;700&quot;}</p>", "<p><img src=\"x.png\" alt=\"x\" width=\"700\"></p>")]
    [Arguments("<p><img src=\"x.png\" alt=\"x\">{width=&#34;700&#34;}</p>", "<p><img src=\"x.png\" alt=\"x\" width=\"700\"></p>")]
    [Arguments("<p><img src=\"x.png\" alt=\"x\">{width=&#x22;700&#x22;}</p>", "<p><img src=\"x.png\" alt=\"x\" width=\"700\"></p>")]
    [Arguments("<p><img src=\"x.png\" alt=\"x\">{width=&#X22;700&#X22;}</p>", "<p><img src=\"x.png\" alt=\"x\" width=\"700\"></p>")]
    [Arguments("<p>The phrase &quot;hi&quot; stays intact.</p>", "<p>The phrase &quot;hi&quot; stays intact.</p>")]
    [Arguments("<p>&quot;a&quot; {: title=&quot;t&quot; } &quot;b&quot;</p>", "<p title=\"t\">&quot;a&quot; &quot;b&quot;</p>")]
    [Arguments("<p><img src=\"x.png\" alt=\"&lt;b title=&quot;F&quot;&gt;x\">{ width=\"700\" }</p>", "<p><img src=\"x.png\" alt=\"&lt;b title=&quot;F&quot;&gt;x\" width=\"700\"></p>")]
    [Arguments("<p><em>x</em>{: .a }<img src=\"y\">{: .b }</p>", "<p><em class=\"a\">x</em><img src=\"y\" class=\"b\"></p>")]
    [Arguments("<h3><em>x</em>{: .a } and <a href=\"z\">l</a>{: .q } {: #hh }</h3>", "<h3 id=\"hh\"><em class=\"a\">x</em> and <a href=\"z\" class=\"q\">l</a></h3>")]
    [Arguments("<h1>&quot;q&quot; {: #x title=&quot;a&quot; }</h1>", "<h1 id=\"x\" title=\"a\">&quot;q&quot;</h1>")]
    [Arguments("<p>&amp; {key=&quot;v&amp;w&quot;}</p>", "<p key=\"v&amp;amp;w\">&amp;</p>")]
    public async Task RewritesBoundaryBlockAndInlineShapes(string html, string expected) =>
        await Assert.That(Rewrite(html)).IsEqualTo(expected);

    /// <summary>Quote entities decode only inside braces, and stray braces pass through.</summary>
    /// <param name="html">Rendered page HTML.</param>
    /// <param name="expected">Expected rewritten HTML.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("<p>no braces &quot;q&quot;</p>", "<p>no braces &quot;q&quot;</p>")]
    [Arguments("<p>{&quot;a&quot;}</p>", "<p>{\"a\"}</p>")]
    [Arguments("<p>x {&#34;y&#x22;} z</p>", "<p>x {\"y\"} z</p>")]
    [Arguments("<p>&quot;out&quot; {: #a } {&quot;in&quot;}</p>", "<p id=\"a\">&quot;out&quot; {\"in\"}</p>")]
    [Arguments("{", "{")]
    [Arguments("}", "}")]
    [Arguments("{}", "{}")]
    [Arguments("{:}", "{:}")]
    [Arguments("{: }", "{: }")]
    [Arguments("<p>{\t.a}</p>", "<p class=\"a\"></p>")]
    [Arguments("<p>a {\n#b }</p>", "<p id=\"b\">a</p>")]
    [Arguments("<p>\u00e9 {: #\u00e9 } \u00fc</p>", "<p id=\"\u00e9\">\u00e9 \u00fc</p>")]
    [Arguments("<td>c {: .x }</td><th>h {: .y }</th>", "<td class=\"x\">c</td><th class=\"y\">h</th>")]
    [Arguments("<dd>d {: .z }</dd><dt>t {: .w }</dt>", "<dd class=\"z\">d</dd><dt class=\"w\">t</dt>")]
    [Arguments("<blockquote>q {: .v }</blockquote>", "<blockquote class=\"v\">q</blockquote>")]
    [Arguments("<p><a href=\"x\">a</a>{: .a}<a href=\"y\">b</a>{: .b}</p>", "<p><a href=\"x\" class=\"a\">a</a><a href=\"y\" class=\"b\">b</a></p>")]
    [Arguments("<p><code>c</code>{: .a}<kbd>k</kbd>{: .b}<mark>m</mark>{: .c}</p>", "<p><code class=\"a\">c</code><kbd class=\"b\">k</kbd><mark class=\"c\">m</mark></p>")]
    [Arguments("<p><a>unclosed{: .a}</p>", "<p class=\"a\"><a>unclosed</p>")]
    [Arguments("<p>{: .a}{: .b}</p>", "<p class=\"a\">{: .b}</p>")]
    [Arguments("<br>{: .a }<hr>{: .b }<input>{: .c }", "<br class=\"a\"><hr class=\"b\"><input class=\"c\">")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task HandlesQuoteEntitiesAndStrayBraces(string html, string expected) =>
        RewritesBoundaryBlockAndInlineShapes(html, expected);

    /// <summary>Pages far larger than the pooled buffers rewrite every repetition of the unit.</summary>
    /// <param name="unit">Page fragment repeated to build the page.</param>
    /// <param name="expectedUnit">Expected rewrite of one fragment.</param>
    /// <param name="repetitions">Number of repetitions.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(HeadingUnit, HeadingUnitRewritten, 1)]
    [Arguments(HeadingUnit, HeadingUnitRewritten, 10)]
    [Arguments(HeadingUnit, HeadingUnitRewritten, 1000)]
    [Arguments(HeadingUnit, HeadingUnitRewritten, LargePageRepetitions)]
    [Arguments(EntityUnit, EntityUnitRewritten, LargePageRepetitions)]
    [Arguments(PlainUnit, PlainUnit, LargePageRepetitions)]
    public async Task RewritesEveryRepetitionOfLargePage(string unit, string expectedUnit, int repetitions)
    {
        var output = Rewrite(Repeat(unit, repetitions));
        await Assert.That(output).IsEqualTo(Repeat(expectedUnit, repetitions));
    }

    /// <summary>A page that mixes rewritten and untouched fragments keeps their order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task KeepsFragmentOrderOnMixedPage()
    {
        const string Page = $"{PlainUnit}{HeadingUnit}{PlainUnit}{EntityUnit}{HeadingUnit}{PlainUnit}";
        const string Expected = $"{PlainUnit}{HeadingUnitRewritten}{PlainUnit}{EntityUnitRewritten}{HeadingUnitRewritten}{PlainUnit}";
        await Assert.That(Rewrite(Page)).IsEqualTo(Expected);
    }

    /// <summary>Rewriting different pages back to back never leaks bytes from an earlier page.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReusedBuffersDoNotLeakEarlierPages()
    {
        var large = Repeat(HeadingUnit, ReusePageRepetitions);
        var largeExpected = Repeat(HeadingUnitRewritten, ReusePageRepetitions);
        for (var i = 0; i < ReuseRounds; i++)
        {
            await Assert.That(Rewrite(large)).IsEqualTo(largeExpected);
            await Assert.That(Rewrite(HeadingUnit)).IsEqualTo(HeadingUnitRewritten);
            await Assert.That(Rewrite(EntityUnit)).IsEqualTo(EntityUnitRewritten);
            await Assert.That(Rewrite(PlainUnit)).IsEqualTo(PlainUnit);
            await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);
        }
    }

    /// <summary>Bytes already in the sink are preserved ahead of the rewritten page.</summary>
    /// <param name="html">Rendered page HTML.</param>
    /// <param name="expected">Expected rewritten HTML.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(PlainUnit, PlainUnit)]
    [Arguments(HeadingUnit, HeadingUnitRewritten)]
    [Arguments(EntityUnit, EntityUnitRewritten)]
    public async Task AppendsAfterExistingSinkContent(string html, string expected)
    {
        ArrayBufferWriter<byte> sink = new();
        sink.Write("PREFIX|"u8);
        AttrListRewriter.RewriteInto(Encode(html), sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo($"PREFIX|{expected}");
    }

    /// <summary>A rewrite started while another rewrite of the same thread is in flight leaves both results intact.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NestedRewriteLeavesBothResultsIntact()
    {
        ArrayBufferWriter<byte> nestedSink = new();
        ArrayBufferWriter<byte> outerSink = new();
        NestedRewriteSink sink = new(outerSink, Encode(EntityUnit), nestedSink);

        AttrListRewriter.RewriteInto(Encode(HeadingUnit), sink);

        await Assert.That(sink.NestedRuns).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(nestedSink.WrittenSpan)).IsEqualTo(EntityUnitRewritten);
        await Assert.That(Encoding.UTF8.GetString(outerSink.WrittenSpan)).IsEqualTo(HeadingUnitRewritten);
    }

    /// <summary>Concurrent rewrites on many threads each produce their own page's output.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConcurrentRewritesDoNotInterfere()
    {
        var heading = Repeat(HeadingUnit, ConcurrentPageRepetitions);
        var headingExpected = Repeat(HeadingUnitRewritten, ConcurrentPageRepetitions);
        var entity = Repeat(EntityUnit, ConcurrentPageRepetitions);
        var entityExpected = Repeat(EntityUnitRewritten, ConcurrentPageRepetitions);

        var workers = new Task<int>[TotalWorkers];
        for (var i = 0; i < WorkersPerShape; i++)
        {
            workers[i] = Task.Run(() => CountMismatches(heading, headingExpected));
            workers[i + WorkersPerShape] = Task.Run(() => CountMismatches(entity, entityExpected));
        }

        var mismatches = await Task.WhenAll(workers);
        var total = 0;
        for (var i = 0; i < mismatches.Length; i++)
        {
            total += mismatches[i];
        }

        await Assert.That(total).IsEqualTo(0);
    }

    /// <summary>Repeats <paramref name="unit"/> <paramref name="count"/> times.</summary>
    /// <param name="unit">Fragment to repeat.</param>
    /// <param name="count">Repetition count.</param>
    /// <returns>The concatenated page.</returns>
    private static string Repeat(string unit, int count)
    {
        StringBuilder sb = new(unit.Length * count);
        for (var i = 0; i < count; i++)
        {
            _ = sb.Append(unit);
        }

        return sb.ToString();
    }

    /// <summary>Rewrites <paramref name="html"/> repeatedly and counts outputs that differ from <paramref name="expected"/>.</summary>
    /// <param name="html">Rendered page HTML.</param>
    /// <param name="expected">Expected rewritten HTML.</param>
    /// <returns>Number of mismatching outputs.</returns>
    private static int CountMismatches(string html, string expected)
    {
        const int Iterations = 300;
        var mismatches = 0;
        for (var i = 0; i < Iterations; i++)
        {
            if (!string.Equals(Rewrite(html), expected, StringComparison.Ordinal))
            {
                mismatches++;
            }
        }

        return mismatches;
    }

    /// <summary>Encodes <paramref name="value"/> as UTF-8.</summary>
    /// <param name="value">Text to encode.</param>
    /// <returns>The UTF-8 bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] Encode(string value) => Encoding.UTF8.GetBytes(value);

    /// <summary>Runs the rewriter and returns the string result.</summary>
    /// <param name="source">HTML input.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        AttrListRewriter.RewriteInto(Encode(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }

    /// <summary>Sink that runs a second rewrite the first time the outer rewrite asks it for space.</summary>
    /// <param name="outer">Sink that receives the outer rewrite.</param>
    /// <param name="nestedHtml">Page rewritten while the outer rewrite is in flight.</param>
    /// <param name="nestedSink">Sink that receives the nested rewrite.</param>
    private sealed class NestedRewriteSink(ArrayBufferWriter<byte> outer, byte[] nestedHtml, ArrayBufferWriter<byte> nestedSink) : IBufferWriter<byte>
    {
        /// <summary>Gets the number of nested rewrites that ran.</summary>
        public int NestedRuns { get; private set; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count) => outer.Advance(count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> GetMemory(int sizeHint) => outer.GetMemory(sizeHint);

        /// <inheritdoc/>
        public Span<byte> GetSpan(int sizeHint)
        {
            if (NestedRuns is 0)
            {
                NestedRuns++;
                AttrListRewriter.RewriteInto(nestedHtml, nestedSink);
            }

            return outer.GetSpan(sizeHint);
        }
    }
}
