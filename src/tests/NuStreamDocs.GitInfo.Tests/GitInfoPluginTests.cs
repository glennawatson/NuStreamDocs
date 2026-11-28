// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Sitemap;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.GitInfo.Tests;

/// <summary>HTML rewriter, sitemap enrichment, and full <see cref="DocBuilder"/> integration tests for <see cref="GitInfoPlugin"/>.</summary>
public class GitInfoPluginTests
{
    /// <summary>Shared guide directory name.</summary>
    private const string GuideDir = "guide";

    /// <summary>Gets a short paragraph HTML fragment reused across tests.</summary>
    private static ReadOnlySpan<byte> SampleParagraph => "<p>x</p>"u8;

    /// <summary>Full site build injects <c>dcterms.modified</c>, renders the contributor footer, ships CSS, and enriches <c>sitemap.xml</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FullBuildInjectsMetaFooterStylesheetAndSitemapLastmod()
    {
        DirectoryPath tempRoot = Path.Combine(Path.GetTempPath(), $"nd-gitfull-{Guid.NewGuid():N}");
        var docsDir = tempRoot / "docs";
        var siteDir = tempRoot / "site";
        _ = (docsDir / GuideDir).Create();

        try
        {
            await docsDir.File("index.md").WriteAllBytesAsync("# Home\n"u8.ToArray());
            await (docsDir / GuideDir).File("intro.md").WriteAllBytesAsync("# Intro\n"u8.ToArray());
            await docsDir.File("untracked.md").WriteAllBytesAsync("# Untracked\n"u8.ToArray());

            FixedLogProvider logProvider = new("\u001e2026-11-28\u001fAlice\nindex.md\nguide/intro.md\n\u001e2026-11-20\u001fBob\nguide/intro.md\n"u8.ToArray());

            await new DocBuilder()
                .WithInput(docsDir)
                .WithOutput(siteDir)
                .WithSiteName("Git Docs"u8)
                .WithSiteUrl("https://docs.example.test"u8)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseSitemap()
                .UseGitInfo(o => o.WithLogProvider(logProvider))
                .BuildAsync();

            var introHtml = Encoding.UTF8.GetString(await (siteDir / GuideDir / "intro").File("index.html").ReadAllBytesAsync());
            var untrackedHtml = Encoding.UTF8.GetString(await (siteDir / "untracked").File("index.html").ReadAllBytesAsync());
            var sitemapXml = Encoding.UTF8.GetString(await siteDir.File("sitemap.xml").ReadAllBytesAsync());
            var cssExists = (siteDir / "assets" / "extensions").File("git-info.css").Exists();

            await Assert.That(introHtml).Contains("<meta name=\"dcterms.modified\" content=\"2026-11-28\">");
            await Assert.That(introHtml).Contains("Contributors: Alice, Bob");
            await Assert.That(untrackedHtml).DoesNotContain("nd-git-info");
            await Assert.That(sitemapXml).Contains("<loc>https://docs.example.test/guide/intro/</loc><lastmod>2026-11-28</lastmod>");
            await Assert.That(sitemapXml).Contains("<loc>https://docs.example.test/./</loc><lastmod>2026-11-28</lastmod>");
            await Assert.That(cssExists).IsTrue();
        }
        finally
        {
            if (tempRoot.Exists())
            {
                tempRoot.DeleteRecursive(true);
            }
        }
    }

    /// <summary>HTML insertion fallbacks, sitemap edge cases, and disabled plugin toggles cover all remaining branches.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlFallbacksSitemapEdgeCasesAndPluginTogglesCoverAllBranches()
    {
        GitPageMetadata metaNoAuthors = new(SampleDateBytes(), []);
        GitPageMetadata metaOneAuthor = new(SampleDateBytes(), [[.. "Alice"u8]]);
        var mainHtml = Rewrite("<main><p>x</p></main>"u8, metaNoAuthors, GitInfoOptions.Default);
        var bodyHtml = Rewrite("<body><p>x</p></body>"u8, metaOneAuthor, GitInfoOptions.Default.WithEmitContributors(false));
        var fragHtml = Rewrite(SampleParagraph, metaNoAuthors, GitInfoOptions.Default.WithEmitMetaTag(false));
        var metaOnlyHtml = Rewrite("<head></head>"u8, metaNoAuthors, GitInfoOptions.Default.WithEmitFooter(false));
        var noopHtml = Rewrite(SampleParagraph, metaNoAuthors, GitInfoOptions.Default.WithEmitMetaTag(false).WithEmitFooter(false));

        Dictionary<byte[], byte[]> dates = [with(ByteArrayComparer.Instance)];
        dates[""u8.ToArray()] = SampleDateBytes();
        var xmlInput = "<urlset><url><loc>https://other.test/a</loc></url><url><loc>https://ex.test/./</loc></url><url><loc>https://ex.test/miss</loc></url><url><loc>unclosed"u8;
        var enriched = Encoding.UTF8.GetString(GitInfoHtmlRewriter.EnrichSitemapBytes(xmlInput, "https://ex.test/"u8, dates));

        Dictionary<byte[], byte[]> emptyDates = [with(ByteArrayComparer.Instance)];
        var unmatchedRoot = Encoding.UTF8.GetString(GitInfoHtmlRewriter.EnrichSitemapBytes("<loc>https://ex.test/</loc>"u8, "https://ex.test/"u8, emptyDates));

        await VerifyPluginLifecycleEdgeCasesAsync();

        await Assert.That(mainHtml).Contains("</footer></main>");
        await Assert.That(bodyHtml).Contains("</footer></body>");
        await Assert.That(fragHtml).EndsWith("</footer>");
        await Assert.That(metaOnlyHtml).Contains("dcterms.modified");
        await Assert.That(noopHtml).StartsWith("<p>");
        await Assert.That(enriched).Contains("<loc>https://ex.test/./</loc><lastmod>2026-11-28</lastmod>");
        await Assert.That(unmatchedRoot).DoesNotContain("lastmod");
    }

    /// <summary>Returns UTF-8 sample date bytes.</summary>
    /// <returns>UTF-8 date bytes.</returns>
    private static byte[] SampleDateBytes() => [.. "2026-11-28"u8];

    /// <summary>Rewrites an HTML snippet using <see cref="GitInfoHtmlRewriter.RewritePage"/>.</summary>
    /// <param name="html">Input HTML bytes.</param>
    /// <param name="metadata">Page metadata.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>Rewritten HTML string.</returns>
    private static string Rewrite(ReadOnlySpan<byte> html, in GitPageMetadata metadata, in GitInfoOptions options)
    {
        ArrayBufferWriter<byte> sink = new();
        GitInfoHtmlRewriter.RewritePage(html, metadata, options, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }

    /// <summary>Exercises <see cref="GitInfoPlugin"/> properties, <see cref="GitInfoPlugin.NeedsRewrite"/>, and <see cref="GitInfoPlugin.FinalizeAsync"/> guards.</summary>
    /// <returns>Async assertion task.</returns>
    private static async Task VerifyPluginLifecycleEdgeCasesAsync()
    {
        DirectoryPath missingDir = Path.Combine(Path.GetTempPath(), $"nd-gitlife-{Guid.NewGuid():N}");
        var disabled = GitInfoOptions.Default
            .WithEmitFooter(false)
            .WithEmitMetaTag(false)
            .WithEnrichSitemap(false)
            .WithIncludeStylesheet(false);

        GitInfoPlugin disabledPlugin = new(disabled);
        ArrayBufferWriter<byte> headSink = new();
        disabledPlugin.WriteHeadExtra(headSink);

        await Assert.That(GitInfoPlugin.NormalizeBaseUrl("   "u8)).IsNull();
        await Assert.That(GitInfoPlugin.NormalizeBaseUrl("https://a.test/"u8)).IsNotNull();
        await Assert.That(disabledPlugin.NeedsRewrite(default)).IsFalse();
        await Assert.That(disabledPlugin.NeedsRewrite(SampleParagraph)).IsFalse();
        await Assert.That(new GitInfoPlugin(disabled.WithEmitMetaTag(true)).NeedsRewrite(SampleParagraph)).IsTrue();
        await Assert.That(new GitInfoPlugin(disabled.WithEnrichSitemap(true)).NeedsRewrite(SampleParagraph)).IsTrue();

        await disabledPlugin.ConfigureAsync(new(missingDir, missingDir, [disabledPlugin], new()), CancellationToken.None);
        await disabledPlugin.FinalizeAsync(new(missingDir, [disabledPlugin]), CancellationToken.None);

        FixedLogProvider logProvider = new("\u001e2026-11-28\u001fAlice\nindex.md\n"u8.ToArray());
        GitInfoPlugin noUrlPlugin = new(GitInfoOptions.Default.WithLogProvider(logProvider));
        await noUrlPlugin.ConfigureAsync(new(missingDir, missingDir, [noUrlPlugin], new()), CancellationToken.None);
        await noUrlPlugin.FinalizeAsync(new(missingDir, [noUrlPlugin]), CancellationToken.None);

        await noUrlPlugin.ConfigureAsync(new(missingDir, missingDir, [noUrlPlugin], new()) { SiteUrl = [.. "https://a.test"u8] }, CancellationToken.None);
        await noUrlPlugin.FinalizeAsync(new(missingDir, [noUrlPlugin]), CancellationToken.None);
        ArrayBufferWriter<byte> pageOut = new();
        noUrlPlugin.PostRender(new("index.md", "# Home"u8, SampleParagraph, pageOut));
        await noUrlPlugin.FinalizeAsync(new(missingDir, [noUrlPlugin]), CancellationToken.None);

        var builderChain = new DocBuilder().UseGitInfo().UseGitInfo(disabled);
        var nameMatches = disabledPlugin.Name.SequenceEqual("git-info"u8);

        const int ExpectedTiebreak = 50;
        await Assert.That(nameMatches).IsTrue();
        await Assert.That(disabledPlugin.ConfigurePriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(disabledPlugin.PostRenderPriority).IsEqualTo(new(PluginBand.Latest, ExpectedTiebreak));
        await Assert.That(disabledPlugin.FinalizePriority).IsEqualTo(new(PluginBand.Latest));
        await Assert.That(disabledPlugin.StaticAssets.Length).IsEqualTo(0);
        await Assert.That(headSink.WrittenCount).IsEqualTo(0);
        await Assert.That(builderChain).IsNotNull();
    }

    /// <summary>Deterministic in-memory <see cref="IGitLogProvider"/> for integration tests.</summary>
    /// <param name="bytes">Canned <c>git log</c> output.</param>
    [System.Diagnostics.DebuggerDisplay("FixedLogProvider")]
    private sealed class FixedLogProvider(byte[] bytes) : IGitLogProvider
    {
        /// <inheritdoc/>
        public ValueTask<byte[]> ReadGitLogAsync(DirectoryPath inputRoot, CancellationToken cancellationToken)
        {
            _ = inputRoot;
            _ = cancellationToken;
            return ValueTask.FromResult(bytes);
        }
    }
}
