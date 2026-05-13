// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Audit;
using NuStreamDocs.Common;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-page cost of the build-time accessibility / performance audit. Every enabled lint runs over
/// every emitted <c>.html</c> file at finalize, so the per-page cost multiplies through large
/// corpora. Two payload shapes — a clean page that fires nothing and a flawed page that trips most
/// lints — across a range of page sizes.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class AuditBenchmarks
{
    /// <summary>Bytes per kilobyte; converts the <c>PageSizeKb</c> param into a byte budget.</summary>
    private const int BytesPerKb = 1024;

    /// <summary>Smallest synthesized page (~4 KB) — typical short doc page.</summary>
    private const int SmallPageKb = 4;

    /// <summary>Mid-range synthesized page (~32 KB) — typical guide / API page.</summary>
    private const int MediumPageKb = 32;

    /// <summary>Repeated body block for the clean fixture — alt + dimensions, link text, ordered headings.</summary>
    private const string CleanBlock =
        "<h2>Section</h2><p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>" +
        "<img src=\"img.png\" alt=\"An image\" width=\"640\" height=\"480\">" +
        "<a href=\"/page\">Read the page</a><button>Submit</button><div tabindex=\"0\">box</div>";

    /// <summary>Repeated body block for the flawed fixture — missing alt / dimensions, empty link, empty button, positive tabindex, skipped heading.</summary>
    private const string FlawedBlock =
        "<h3>Section</h3><p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>" +
        "<img src=\"img.png\"><a href=\"/page\"><i class=\"icon-x\"></i></a>" +
        "<button><span class=\"c\"></span></button><div tabindex=\"3\">box</div>";

    /// <summary>Site-relative page URL passed to the auditor (label only).</summary>
    private static readonly UrlPath PageUrl = (UrlPath)"bench/page.html";

    /// <summary>Pre-built page bytes for the current params.</summary>
    private byte[] _html = [];

    /// <summary>Gets or sets the synthetic page size in kilobytes.</summary>
    [Params(SmallPageKb, MediumPageKb)]
    public int PageSizeKb { get; set; }

    /// <summary>Gets or sets a value indicating whether the page trips lints (true) or is clean (false).</summary>
    [Params(false, true)]
    public bool Flawed { get; set; }

    /// <summary>Generates the HTML fixture for the current params.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var budget = PageSizeKb * BytesPerKb;
        var block = Flawed ? FlawedBlock : CleanBlock;
        StringBuilder body = new(budget);
        for (var written = 0; written < budget; written += block.Length)
        {
            body.Append(block);
        }

        StringBuilder page = new(body.Length + 256);
        page.Append(Flawed
                ? "<!DOCTYPE html><html><head><title></title>"
                : "<!DOCTYPE html><html lang=\"en\"><head><title>Bench</title>")
            .Append(Flawed
                ? "<script src=\"/blocking.js\"></script>"
                : "<meta name=\"viewport\" content=\"width=device-width\">")
            .Append("</head><body><h1>Heading</h1>")
            .Append(body)
            .Append("</body></html>");
        _html = Encoding.UTF8.GetBytes(page.ToString());
    }

    /// <summary>Runs every default-enabled lint over the page.</summary>
    /// <returns>The number of findings.</returns>
    [Benchmark]
    public int Audit() => PageAuditor.Audit(PageUrl, _html, AuditOptions.Default).Length;
}
