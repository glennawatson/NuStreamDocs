// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.Glossary.Tests;

/// <summary>Tests for <see cref="GlossaryPlugin"/>, <see cref="GlossaryLoader"/>, <see cref="GlossaryHtmlRewriter"/>, and <see cref="GlossaryOptionsExtensions"/>.</summary>
public sealed class GlossaryPluginTests
{
    /// <summary>Index HTML filename used in site output assertions.</summary>
    private const string IndexHtml = "index.html";

    /// <summary>Custom glossary subdirectory segment.</summary>
    private const string TermsSegment = "terms";

    /// <summary>Default glossary YAML filename.</summary>
    private const string GlossaryYamlName = "glossary.yml";

    /// <summary>Expected merged glossary entry count in the loader test.</summary>
    private const int ExpectedMergedTermCount = 4;

    /// <summary>Verifies option fluent extensions and argument guards on glossary options, plugin, and builder methods.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OptionsAndBuilderGuards_InvalidInputs_Throw()
    {
        GlossaryOptions? nullOpts = null;
        DocBuilder? nullBuilder = null;

        await Assert.That(() => nullOpts!.WithGlossaryFile("a.yml")).Throws<ArgumentNullException>();
        await Assert.That(() => nullOpts!.WithOutputSubdirectory(TermsSegment)).Throws<ArgumentNullException>();
        await Assert.That(() => nullOpts!.WithPageTitle("Terms"u8)).Throws<ArgumentNullException>();
        await Assert.That(static () => GlossaryOptions.Default.WithPageTitle(default)).Throws<ArgumentException>();
        await Assert.That(() => nullOpts!.AddTerm("API"u8, "Def"u8)).Throws<ArgumentNullException>();
        await Assert.That(static () => GlossaryOptions.Default.AddTerm(default, "Def"u8)).Throws<ArgumentException>();
        await Assert.That(static () => GlossaryOptions.Default.AddTerm("API"u8, default)).Throws<ArgumentException>();
        await Assert.That(static () => new GlossaryPlugin(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UseGlossary()).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UseGlossary(GlossaryOptions.Default)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UseGlossary((GlossaryOptions)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UseGlossary(static o => o)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UseGlossary((Func<GlossaryOptions, GlossaryOptions>)null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies YAML map, JSON array, empty files, rooted/unrooted paths, slug edge cases, and longest-term-first sorting in <see cref="GlossaryLoader"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadAsync_YamlMapAndJsonArrayAndProgrammaticTerms_MergesAndSorts(CancellationToken cancellationToken)
    {
        await Assert.That(Encoding.UTF8.GetString(GlossaryLoader.Slugify("!!!"u8))).IsEqualTo("term");
        await Assert.That(Encoding.UTF8.GetString(GlossaryLoader.Slugify("JSON Schema!"u8))).IsEqualTo("json-schema");

        var dir = Directory.CreateTempSubdirectory("nstd-glossary-load-");
        try
        {
            var yamlFile = Path.Combine(dir.FullName, GlossaryYamlName);
            await File.WriteAllTextAsync(yamlFile, "JSON: JavaScript Object Notation\nJSON Schema: Declarative JSON validator\n", cancellationToken);

            var opts = GlossaryOptions.Default
                .WithGlossaryFile(GlossaryYamlName)
                .AddTerm("API"u8, "Application Programming Interface"u8)
                .AddTerm("API2"u8, "Prefix variant"u8);

            var (sorted, longest) = await GlossaryLoader.LoadAsync(opts, new(dir.FullName), cancellationToken);
            await Assert.That(sorted.Length).IsEqualTo(ExpectedMergedTermCount);
            await Assert.That(Encoding.UTF8.GetString(sorted[0].Term)).IsEqualTo("API");
            await Assert.That(Encoding.UTF8.GetString(sorted[1].Term)).IsEqualTo("API2");
            await Assert.That(Encoding.UTF8.GetString(longest[0].Term)).IsEqualTo("JSON Schema");

            var jsonMapPath = Path.Combine(dir.FullName, "map.json");
            await File.WriteAllTextAsync(jsonMapPath, "{\"UDP\":\"Datagram\",\"skipNum\":42}", cancellationToken);
            var (mapSorted, _) = await GlossaryLoader.LoadAsync(GlossaryOptions.Default.WithGlossaryFile(jsonMapPath), default, cancellationToken);
            await Assert.That(mapSorted).HasSingleItem();

            var jsonArrayPath = Path.Combine(dir.FullName, "terms.json");
            const string jsonArray = """
                [{"term":"CLS","definition":"Cumulative Layout Shift","extra":1},
                 {"term":"","definition":"skip"},{"term":"onlyTerm"},{"term":1,"definition":false},"notObj"]
                """;
            await File.WriteAllTextAsync(jsonArrayPath, jsonArray, cancellationToken);
            var (arrSorted, _) = await GlossaryLoader.LoadAsync(GlossaryOptions.Default.WithGlossaryFile(jsonArrayPath), default, cancellationToken);
            await Assert.That(arrSorted).HasSingleItem();

            var emptyFile = Path.Combine(dir.FullName, "empty.yml");
            await File.WriteAllTextAsync(emptyFile, "   ", cancellationToken);
            var (emptySorted, _) = await GlossaryLoader.LoadAsync(GlossaryOptions.Default.WithGlossaryFile(emptyFile), default, cancellationToken);
            await Assert.That(emptySorted).IsEmpty();

            var scalarJson = Path.Combine(dir.FullName, "scalar.json");
            await File.WriteAllTextAsync(scalarJson, "123", cancellationToken);
            var (scalarSorted, _) = await GlossaryLoader.LoadAsync(GlossaryOptions.Default.WithGlossaryFile(scalarJson), default, cancellationToken);
            await Assert.That(scalarSorted).IsEmpty();

            var (missingRelNoRoot, _) = await GlossaryLoader.LoadAsync(GlossaryOptions.Default.WithGlossaryFile("missing-rel.yml"), default, cancellationToken);
            await Assert.That(missingRelNoRoot).IsEmpty();

            var (noFileSorted, _) = await GlossaryLoader.LoadAsync(GlossaryOptions.Default.WithGlossaryFile(default), default, cancellationToken);
            await Assert.That(noFileSorted).IsEmpty();
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>Verifies HTML auto-linking skips headings, links, code, scripts, styles, abbrs, and self-closing/comment tags while escaping attributes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rewrite_ComplexHtml_SkipsForbiddenElementsAndEscapesAttributes()
    {
        GlossaryEntry[] entries =
        [
            new("UTF-8"u8.ToArray(), "8-bit & \"quoted\" <unit> > text"u8.ToArray(), "utf-8"u8.ToArray()),
            new("API"u8.ToArray(), "Interface"u8.ToArray(), "api"u8.ToArray()),
        ];

        await Assert.That(GlossaryHtmlRewriter.NeedsRewrite(default, entries)).IsFalse();
        await Assert.That(GlossaryHtmlRewriter.NeedsRewrite("<p>x</p>"u8, [])).IsFalse();

        var input = """
            <>
            <!DOCTYPE html><?pi?>
            <head><title>UTF-8</title><style>.utf-8{}</style><script>const x = "UTF-8";</script></head>
            <body>
            <h1>UTF-8 Heading</h1><h0>x</h0><h7><em>API allowed in non-heading</em></h7>
            <p>Use UTF-8 and utf-8 and UTF-8_ext and preUTF-8 with <img src="x" /><code>UTF-8</code> <pre>API</pre> <a href="#">API</a> <abbr>API</abbr></code></div> trailing UTF-8<unclosed
            """u8;

        ArrayBufferWriter<byte> firstOnlyOut = new();
        GlossaryHtmlRewriter.Rewrite(input, entries, default, true, firstOnlyOut);
        var firstOnlyHtml = Encoding.UTF8.GetString(firstOnlyOut.WrittenSpan);
        await Assert.That(firstOnlyHtml).Contains("<abbr class=\"nd-glossary-term\" tabindex=\"0\" title=\"8-bit &amp; &quot;quoted&quot; &lt;unit&gt; &gt; text\">UTF-8</abbr>");
        await Assert.That(firstOnlyHtml).Contains("and utf-8 and UTF-8_ext");
        await Assert.That(firstOnlyHtml).Contains("<h1>UTF-8 Heading</h1>");
        await Assert.That(firstOnlyHtml).Contains("<code>UTF-8</code>");
        await Assert.That(firstOnlyHtml).Contains("<unclosed");

        ArrayBufferWriter<byte> repeatOut = new();
        GlossaryHtmlRewriter.Rewrite("<p>API and api</p>plain tail"u8, entries, "/glossary/"u8, false, repeatOut);
        var repeatHtml = Encoding.UTF8.GetString(repeatOut.WrittenSpan);
        await Assert.That(repeatHtml).Contains("data-glossary-href=\"/glossary/#api\">API</abbr> and <abbr");
    }

    /// <summary>Verifies directory-URL <see cref="DocBuilder"/> builds synthesize the glossary page and auto-link prose.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocBuilder_DirectoryUrlModes_SynthesizesGlossaryAndAutoLinksProse(CancellationToken cancellationToken)
    {
        var dir = Directory.CreateTempSubdirectory("nstd-glossary-dir-");
        try
        {
            var docs = Directory.CreateDirectory(Path.Combine(dir.FullName, "docs"));
            var out1 = Path.Combine(dir.FullName, "site1");
            var out2 = Path.Combine(dir.FullName, "site2");
            await File.WriteAllTextAsync(Path.Combine(docs.FullName, "guide.md"), "# Guide\n\n NuStreamDocs uses UTF-8 everywhere.\n", cancellationToken);

            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(out1)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseGlossary(static o => o
                    .WithOutputSubdirectory(TermsSegment)
                    .WithPageTitle("Project \"Terms\" \\ Index"u8)
                    .AddTerm("UTF-8"u8, "Variable-width Unicode encoding."u8))
                .BuildAsync(cancellationToken);

            var guideHtml = await File.ReadAllTextAsync(Path.Combine(out1, "guide", IndexHtml), cancellationToken);
            var glossaryHtml = await File.ReadAllTextAsync(Path.Combine(out1, TermsSegment, IndexHtml), cancellationToken);
            await Assert.That(guideHtml).Contains("data-glossary-href=\"/terms/#utf-8\">UTF-8</abbr>");
            await Assert.That(guideHtml).Contains("/assets/stylesheets/nstd-glossary.css");
            await Assert.That(File.Exists(Path.Combine(out1, "assets", "stylesheets", "nstd-glossary.css"))).IsTrue();
            await Assert.That(glossaryHtml).DoesNotContain("nd-glossary-term");

            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(out2)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseGlossary(GlossaryOptions.Default with { OutputSubdirectory = default })
                .BuildAsync(cancellationToken);
            await Assert.That(File.Exists(Path.Combine(out2, IndexHtml))).IsFalse();
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>Verifies file-URL and disabled-page <see cref="DocBuilder"/> glossary configurations.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocBuilder_FileUrlAndDisabledModes_BehaveDeterministically(CancellationToken cancellationToken)
    {
        var dir = Directory.CreateTempSubdirectory("nstd-glossary-file-");
        try
        {
            var docs = Directory.CreateDirectory(Path.Combine(dir.FullName, "docs"));
            await File.WriteAllTextAsync(Path.Combine(docs.FullName, "guide.md"), "# Guide\n\nCLS metric.\n", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(docs.FullName, GlossaryYamlName), "CLS: Cumulative Layout Shift\n", cancellationToken);

            var outRoot = Path.Combine(dir.FullName, "root");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outRoot)
                .UseMaterial3Theme()
                .UseGlossary(GlossaryOptions.Default with { OutputSubdirectory = default })
                .BuildAsync(cancellationToken);
            await Assert.That(File.Exists(Path.Combine(outRoot, IndexHtml))).IsTrue();

            var outDisabled = Path.Combine(dir.FullName, "disabled");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outDisabled)
                .UseMaterial3Theme()
                .UseGlossary(new GlossaryOptions { OutputSubdirectory = new(TermsSegment), GenerateGlossaryPage = false, AutoLinkTerms = false })
                .BuildAsync(cancellationToken);
            await Assert.That(File.Exists(Path.Combine(outDisabled, "terms.html"))).IsFalse();

            var outDefault = Path.Combine(dir.FullName, "default");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outDefault)
                .UseMaterial3Theme()
                .UseGlossary()
                .BuildAsync(cancellationToken);
            await Assert.That(File.Exists(Path.Combine(outDefault, "glossary", IndexHtml))).IsTrue();
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
