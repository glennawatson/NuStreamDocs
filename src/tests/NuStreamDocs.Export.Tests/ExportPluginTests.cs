// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.Export.Tests;

/// <summary>Tests for <see cref="ExportPlugin"/>, chapter extraction, link rewriting, and print bundle output.</summary>
public sealed class ExportPluginTests
{
    /// <summary>Home Markdown filename.</summary>
    private const string IndexMarkdown = "index.md";

    /// <summary>Verifies option fluent methods and argument guards across export options, plugin, and builder extensions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GuardsAndOptions_InvalidAndValidInputs_BehaveDeterministically()
    {
        ExportOptions? nullOpts = null;
        DocBuilder? nullBuilder = null;

        await Assert.That(() => nullOpts!.WithOutputFileName("a.html")).Throws<ArgumentNullException>();
        await Assert.That(static () => ExportOptions.Default.WithOutputFileName(" ")).Throws<ArgumentException>();
        await Assert.That(() => nullOpts!.WithDocumentTitle("Doc"u8)).Throws<ArgumentNullException>();
        await Assert.That(static () => ExportOptions.Default.WithDocumentTitle(default)).Throws<ArgumentException>();
        await Assert.That(() => nullOpts!.WithSubtitle("Sub"u8)).Throws<ArgumentNullException>();
        await Assert.That(() => nullOpts!.WithTableOfContents(false)).Throws<ArgumentNullException>();
        await Assert.That(static () => new ExportPlugin(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UseExport()).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UseExport(ExportOptions.Default)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UseExport((ExportOptions)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UseExport(static o => o)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UseExport((Func<ExportOptions, ExportOptions>)null!)).Throws<ArgumentNullException>();

        var configured = ExportOptions.Default
            .WithOutputFileName("bundle/manual.html")
            .WithDocumentTitle("Manual & <Guide> \"1\""u8)
            .WithSubtitle("Release 1.0"u8)
            .WithTableOfContents(false);
        await Assert.That(configured.OutputFileName).IsEqualTo("bundle/manual.html");
        await Assert.That(configured.IncludeTableOfContents).IsFalse();

        ExportPlugin plugin = new();
        await Assert.That(Encoding.UTF8.GetString(plugin.Name)).IsEqualTo("export");
        await Assert.That(plugin.ScanPriority.Band).IsEqualTo(NuStreamDocs.Plugins.PluginBand.Late);
        await Assert.That(new DocBuilder().UseExport(ExportOptions.Default)).IsNotNull();
    }

    /// <summary>Verifies HTML extraction fallbacks (<c>&lt;article&gt;</c>, <c>&lt;body&gt;</c>, raw fragment, <c>&lt;title&gt;</c>, path fallback) and section ID normalization.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractorAndLinkRewriter_AllBranches_ProduceExpectedOutput()
    {
        await Assert.That(Encoding.UTF8.GetString(ExportHtmlExtractor.BuildSectionId("///"))).IsEqualTo("nd-page-root");
        await Assert.That(Encoding.UTF8.GetString(ExportHtmlExtractor.BuildSectionId("Guide/Setup--Page!.md"))).IsEqualTo("nd-page-guide-setup-page");

        var bodyFallback = ExportHtmlExtractor.Extract(
            "sub\\page.md",
            "sub/page/"u8,
            "<articles>skip</articles><head><title>From Title</title></head><body><p>Body text</p></body>"u8);
        await Assert.That(Encoding.UTF8.GetString(bodyFallback.Title)).IsEqualTo("From Title");
        await Assert.That(Encoding.UTF8.GetString(bodyFallback.BodyHtml)).IsEqualTo("<p>Body text</p>");

        var unclosedCloseTag = ExportHtmlExtractor.Extract("unclosed.md", "unclosed/"u8, "<article><h1>OnlyOpen"u8);
        await Assert.That(Encoding.UTF8.GetString(unclosedCloseTag.Title)).IsEqualTo("unclosed.md");

        var rawFallback = ExportHtmlExtractor.Extract("raw", "raw.html"u8, "<article unclosed"u8);
        await Assert.That(Encoding.UTF8.GetString(rawFallback.Title)).IsEqualTo("raw");
        await Assert.That(Encoding.UTF8.GetString(ExportHtmlExtractor.Extract("eof", "eof.html"u8, "<article"u8).Title)).IsEqualTo("eof");

        byte[] pilcrowPage = [.. "<article><h1><span>Hello</span> "u8, 0xC2, 0xB6, .. "</h1></article>"u8];
        var h1WithNestedSpan = ExportHtmlExtractor.Extract(IndexMarkdown, "/"u8, pilcrowPage);
        await Assert.That(Encoding.UTF8.GetString(h1WithNestedSpan.Title)).IsEqualTo("Hello");

        ExportPageEntry[] entries = [h1WithNestedSpan, bodyFallback, rawFallback];
        var lookup = ExportLinkRewriter.BuildRouteLookup(entries);

        var linksHtml = """
            <a href="../sub/page/index.html#sec">Sub</a>
            <a href="./index.html">Home</a>
            <a href="">Empty</a>
            <a href="#local">Hash</a>
            <a href="https://example.com">Ext</a>
            <a href="//cdn.example.com">Proto</a>
            <a href="unknown.html">Unknown</a>
            <a href="unclosed
            """u8;

        ArrayBufferWriter<byte> outBuf = new();
        ExportLinkRewriter.RewriteLinks(linksHtml, lookup, outBuf);
        var rewritten = Encoding.UTF8.GetString(outBuf.WrittenSpan);
        await Assert.That(rewritten).Contains("href=\"#nd-page-sub-page\"");
        await Assert.That(rewritten).Contains("href=\"#nd-page-index\"");
        await Assert.That(rewritten).Contains("href=\"https://example.com\"");
        await Assert.That(rewritten).Contains("href=\"unclosed");

        await Assert.That(ExportPlugin.CompareEntries(bodyFallback, h1WithNestedSpan)).IsGreaterThan(0);
        await Assert.That(ExportPlugin.CompareEntries(h1WithNestedSpan, bodyFallback)).IsLessThan(0);
        await Assert.That(ExportPlugin.CompareEntries(bodyFallback, rawFallback)).IsGreaterThan(0);
    }

    /// <summary>Verifies plugin guard branches on empty scan/finalize contexts and full <see cref="DocBuilder"/> single-file print bundle generation.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PluginAndDocBuilder_EmptyGuardsAndFullSiteBuild_EmitsPrintBundle(CancellationToken cancellationToken)
    {
        ExportPlugin emptyPlugin = new();
        emptyPlugin.Scan(new(default, default, "<p>ignored</p>"u8));
        await emptyPlugin.FinalizeAsync(new(new("/tmp"), []), cancellationToken);

        emptyPlugin.Scan(new(IndexMarkdown, default, "<article><h1>Home</h1></article>"u8));
        await emptyPlugin.FinalizeAsync(new(default, []), cancellationToken);

        var dir = Directory.CreateTempSubdirectory("nstd-export-");
        try
        {
            var docs = Directory.CreateDirectory(Path.Combine(dir.FullName, "docs"));
            await File.WriteAllTextAsync(Path.Combine(docs.FullName, IndexMarkdown), "# Welcome\n\nSee [Setup](setup.md).\n", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(docs.FullName, "setup.md"), "# Setup & \"Install\" <Guide>\n\nReady.\n", cancellationToken);

            var outDefault = Path.Combine(dir.FullName, "site1");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outDefault)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseExport()
                .BuildAsync(cancellationToken);

            var printHtml = await File.ReadAllTextAsync(Path.Combine(outDefault, "print.html"), cancellationToken);
            await Assert.That(printHtml).Contains("class=\"nd-export-toc\"");
            await Assert.That(printHtml).Contains("id=\"nd-page-index\"");
            await Assert.That(printHtml).Contains("id=\"nd-page-setup\"");
            await Assert.That(printHtml).Contains("href=\"#nd-page-setup\"");

            var outCustom = Path.Combine(dir.FullName, "site2");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outCustom)
                .UseMaterial3Theme()
                .UseExport(static o => o
                    .WithOutputFileName("exports/handbook.html")
                    .WithDocumentTitle("Handbook & <Ref> \"2\""u8)
                    .WithSubtitle("Offline Edition"u8)
                    .WithTableOfContents(false))
                .BuildAsync(cancellationToken);

            var customHtml = await File.ReadAllTextAsync(Path.Combine(outCustom, "exports", "handbook.html"), cancellationToken);
            await Assert.That(customHtml).Contains("Offline Edition");
            await Assert.That(customHtml).DoesNotContain("<nav class=\"nd-export-toc\"");
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
