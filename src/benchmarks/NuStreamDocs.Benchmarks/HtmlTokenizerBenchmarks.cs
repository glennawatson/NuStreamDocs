// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Audit;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-page cost of the byte-level HTML tokenizer (<c>HtmlTagCursor</c>) and attribute parser
/// (<c>HtmlAttr</c>) that back every accessibility lint. Each lint module walks the cursor over the
/// whole page, so the tokenizer's per-byte throughput multiplies several times per audited page;
/// this isolates it from the lint dispatch and the finding-collection churn measured by
/// <see cref="AuditBenchmarks"/>.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class HtmlTokenizerBenchmarks
{
    /// <summary>Bytes per kilobyte; converts the <c>PageSizeKb</c> param into a byte budget.</summary>
    private const int BytesPerKb = 1024;

    /// <summary>Smallest synthesized page (~4 KB).</summary>
    private const int SmallPageKb = 4;

    /// <summary>Mid-range synthesized page (~32 KB).</summary>
    private const int MediumPageKb = 32;

    /// <summary>Repeated body block — a mix of tags, attributes, a comment, and a rawtext (<c>&lt;script&gt;</c>) element.</summary>
    private const string Block =
        "<section class=\"c\"><h2 id=\"s\">Heading</h2><p>Lorem ipsum dolor sit amet, consectetur.</p>" +
        "<img src=\"a.png\" alt=\"An image\" width=\"640\" height=\"480\" loading=\"lazy\">" +
        "<a href=\"/page\" rel=\"next\">link</a><!-- a comment --><script type=\"module\">/* if (a<b) {} */</script></section>";

    /// <summary>A representative <c>&lt;img&gt;</c> attribute run, for the attribute-lookup benchmarks.</summary>
    private static readonly byte[] ImgAttributes =
        [.. "src=\"a.png\" alt=\"An image\" width=\"640\" height=\"480\" loading=\"lazy\" class=\"hero\" decoding=\"async\""u8];

    /// <summary>Pre-built page bytes for the current params.</summary>
    private byte[] _html = [];

    /// <summary>Gets or sets the synthetic page size in kilobytes.</summary>
    [Params(SmallPageKb, MediumPageKb)]
    public int PageSizeKb { get; set; }

    /// <summary>Generates the HTML fixture for the current params.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var budget = PageSizeKb * BytesPerKb;
        StringBuilder builder = new(budget);
        for (var written = 0; written < budget; written += Block.Length)
        {
            builder.Append(Block);
        }

        _html = Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>Walks every tag in the page (the work each lint module repeats).</summary>
    /// <returns>The number of tags walked.</returns>
    [Benchmark]
    public int WalkTags()
    {
        HtmlTagCursor cursor = new(_html);
        var count = 0;
        while (cursor.MoveNext())
        {
            count++;
        }

        return count;
    }

    /// <summary>Looks up an attribute that is present (partial scan).</summary>
    /// <returns><see langword="true"/> (the attribute is present).</returns>
    [Benchmark]
    public bool LookupPresentAttribute() => HtmlAttr.Has(ImgAttributes, "height"u8);

    /// <summary>Looks up an attribute that is absent (full scan of the run).</summary>
    /// <returns><see langword="false"/> (the attribute is absent).</returns>
    [Benchmark]
    public bool LookupAbsentAttribute() => HtmlAttr.Has(ImgAttributes, "data-foo"u8);
}
