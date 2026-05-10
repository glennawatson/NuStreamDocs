// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Search;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Microbenchmark for <see cref="HtmlTextExtractor.Extract"/> — the byte-walker that strips
/// HTML tags out of every rendered page during the parallel render phase, called once per
/// page on the search-scan hot path. Three payload shapes pin the byte-walker: bare prose,
/// realistic mixed (heading + paragraphs + code + list), and chatty (script / style noise
/// the walker has to skip).
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class HtmlTextExtractorBenchmarks
{
    /// <summary>Repetition count used to grow the long payloads past the page-builder pool's small-input fast path.</summary>
    private const int LongPayloadRepeats = 600;

    /// <summary>Half-payload-size hint passed to <see cref="PageBuilderPool.Rent(int)"/> — text output is always smaller than the HTML input because tag bytes get stripped.</summary>
    private const int RentHintDivisor = 2;

    /// <summary>Bare prose with one H1 and a few paragraphs (~300 B).</summary>
    private byte[] _shortHtml = [];

    /// <summary>Realistic mixed payload — headings, paragraphs, code, lists (~30 KB).</summary>
    private byte[] _mixedHtml = [];

    /// <summary>Same shape as <see cref="_mixedHtml"/> but with a fat <c>&lt;script&gt;</c> the walker has to skip.</summary>
    private byte[] _chattyHtml = [];

    /// <summary>Allocates the three pre-canned payloads once per parameter set.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _shortHtml = Encoding.UTF8.GetBytes(
            "<h1>Page Title</h1>"
            + "<p>Short paragraph with <em>emphasis</em> and a <a href=\"x\">link</a>.</p>"
            + "<p>Second paragraph for body coverage.</p>");

        var mixed = new StringBuilder(40 * 1024)
            .Append("<h1>Long Page Title</h1>");
        for (var i = 0; i < LongPayloadRepeats; i++)
        {
            mixed
                .Append("<h2>Section ").Append(i).Append("</h2>")
                .Append("<p>Body paragraph with <code>inline code</code> and a <a href=\"x\">link</a>.</p>")
                .Append("<ul><li>Item one</li><li>Item two</li></ul>");
        }

        _mixedHtml = Encoding.UTF8.GetBytes(mixed.ToString());

        var chatty = new StringBuilder(60 * 1024)
            .Append("<h1>Page With Scripts</h1>")
            .Append("<script>");
        for (var i = 0; i < LongPayloadRepeats; i++)
        {
            chatty.Append("var x").Append(i).Append('=').Append(i).Append(';');
        }

        chatty
            .Append("</script>")
            .Append("<style>body{color:red}.foo{display:none}</style>");
        for (var i = 0; i < LongPayloadRepeats / RentHintDivisor; i++)
        {
            chatty.Append("<p>Visible paragraph ").Append(i).Append(".</p>");
        }

        _chattyHtml = Encoding.UTF8.GetBytes(chatty.ToString());
    }

    /// <summary>Bare-prose extraction — measures the steady-state cost on a tiny page.</summary>
    /// <returns>Title byte count (returned so BenchmarkDotNet doesn't elide the call).</returns>
    [Benchmark]
    public int Short()
    {
        using var rental = PageBuilderPool.Rent(_shortHtml.Length / RentHintDivisor);
        return HtmlTextExtractor.Extract(_shortHtml, rental.Writer).Length;
    }

    /// <summary>Realistic ~30 KB payload — measures the byte walker's per-byte cost on a typical doc page.</summary>
    /// <returns>Title byte count.</returns>
    [Benchmark]
    public int Mixed()
    {
        using var rental = PageBuilderPool.Rent(_mixedHtml.Length / RentHintDivisor);
        return HtmlTextExtractor.Extract(_mixedHtml, rental.Writer).Length;
    }

    /// <summary>Script/style-heavy payload — pins the cost of the script/style skip branch.</summary>
    /// <returns>Title byte count.</returns>
    [Benchmark]
    public int Chatty()
    {
        using var rental = PageBuilderPool.Rent(_chattyHtml.Length / RentHintDivisor);
        return HtmlTextExtractor.Extract(_chattyHtml, rental.Writer).Length;
    }
}
