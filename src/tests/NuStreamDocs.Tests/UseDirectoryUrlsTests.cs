// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Links;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end coverage for the <c>use_directory_urls</c> toggle: output paths, link rewriting, and DocBuilder fluent.</summary>
public class UseDirectoryUrlsTests
{
    /// <summary>Platform-specific separator used to build expected outputs.</summary>
    private static readonly char Sep = Path.DirectorySeparatorChar;

    /// <summary>Flat-URL form: <c>guide/foo.md</c> → <c>out/guide/foo.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FlatUrlsMapNonIndexFiles()
    {
        var path = OutputPathBuilder.ForFlatUrls("/out", "guide/foo.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}guide/foo.html");
    }

    /// <summary>Directory-URL form: <c>guide/foo.md</c> → <c>out/guide/foo/index.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsMapNonIndexFilesToIndexHtml()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "guide/foo.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}guide/foo{Sep}index.html");
    }

    /// <summary>Directory-URL form preserves <c>index.md</c> at its original location.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsKeepIndexMdFlat()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "guide/index.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}guide/index.html");
    }

    /// <summary>Index filename casing does not change the directory URL's output filename.</summary>
    /// <param name="source">The source-relative index path.</param>
    /// <param name="expected">The output path relative to the site root.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Index.md", "index.html")]
    [Arguments("INDEX.md", "index.html")]
    [Arguments("guide/Index.md", "guide/index.html")]
    [Arguments("guide/iNdEx.MD", "guide/index.html")]
    public async Task DirectoryUrlsNormalizeIndexFilename(string source, string expected)
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", source);
        await Assert.That(path.Replace('\\', '/').Value).IsEqualTo($"/out/{expected}");
    }

    /// <summary>Links to index pages resolve to emitted HTML containing the requested anchor.</summary>
    /// <param name="fileName">The index filename casing.</param>
    /// <param name="directory">The source-relative directory containing the index.</param>
    /// <param name="useDirectoryUrls">Whether to emit directory URLs.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task IndexLinksResolveToEmittedAnchor(
        [Matrix("index.md", "Index.md", "INDEX.md", "iNdEx.md")] string fileName,
        [Matrix("", "api/System")] string directory,
        [Matrix(false, true)] bool useDirectoryUrls,
        CancellationToken cancellationToken)
    {
        const int ExpectedPageCount = 2;
        using var fixture = TempBuildFixture.Create();
        var sourceDirectory = Path.Combine(fixture.Input, directory);
        _ = Directory.CreateDirectory(Path.Combine(sourceDirectory, "members"));
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, fileName), "<a id=\"T:System.Index\"></a>\n\n# Index", cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, "members", "FromStart.md"),
            $"[Index](../{fileName}#T:System.Index)",
            cancellationToken);

        var count = await new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .UseDirectoryUrls(useDirectoryUrls)
            .UseMarkdownLinks()
            .BuildAsync(cancellationToken);

        await Assert.That(count).IsEqualTo(ExpectedPageCount);
        var relativeSource = directory.Length is 0 ? fileName : $"{directory}/{fileName}";
        var pageUrl = Encoding.UTF8.GetString(Utf8MarkdownUrl.FromRelativePath(relativeSource, useDirectoryUrls));
        var pageUri = new Uri(new Uri("https://docs.example/"), pageUrl);
        var relativeOutput = pageUri.AbsolutePath.TrimStart('/') + (useDirectoryUrls ? "index.html" : string.Empty);
        var outputPath = Path.Combine(fixture.Output, relativeOutput);
        await Assert.That(File.Exists(outputPath)).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(outputPath, cancellationToken)).Contains("id=\"T:System.Index\"");

        var memberPath = useDirectoryUrls ? "members/FromStart/index.html" : "members/FromStart.html";
        var memberHtml = await File.ReadAllTextAsync(Path.Combine(fixture.Output, directory, memberPath), cancellationToken);
        var expectedLink = useDirectoryUrls ? "../../#T:System.Index" : $"../{Path.ChangeExtension(fileName, ".html")}#T:System.Index";
        await Assert.That(memberHtml).Contains($"href=\"{expectedLink}\"");
        var memberUri = new Uri(new Uri("https://docs.example/"), $"{directory}/{memberPath}".TrimStart('/'));
        var target = new Uri(memberUri, expectedLink);
        await Assert.That(target.AbsolutePath).IsEqualTo(pageUri.AbsolutePath);
        await Assert.That(target.Fragment).IsEqualTo("#T:System.Index");
    }

    /// <summary>Directory-URL form passes non-markdown assets through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsPassNonMarkdownThrough()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "assets/style.css");
        await Assert.That(path).IsEqualTo($"/out{Sep}assets/style.css");
    }

    /// <summary>Top-level <c>foo.md</c> works with directory URLs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsHandleRootStem()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "about.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}about{Sep}index.html");
    }

    /// <summary>Link rewriter (flat): <c>about.md</c> → <c>about.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterFlatRewritesMdToHtml()
    {
        var rewritten = MarkdownLinkRewriter.Rewrite("<a href=\"about.md\">about</a>"u8, false);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"about.html\">about</a>");
    }

    /// <summary>Link rewriter (directory): <c>about.md</c> → <c>about/</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryAppendsSlash()
    {
        var rewritten = MarkdownLinkRewriter.Rewrite("<a href=\"about.md\">about</a>"u8, true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"about/\">about</a>");
    }

    /// <summary>Link rewriter (directory) collapses <c>guide/index.md</c> to <c>guide/</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryCollapsesIndexMd()
    {
        var rewritten = MarkdownLinkRewriter.Rewrite("<a href=\"guide/index.md\">guide</a>"u8, true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"guide/\">guide</a>");
    }

    /// <summary>Link rewriter (directory) collapses bare <c>index.md</c> to the directory root (empty path).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryCollapsesBareIndexMd()
    {
        var rewritten = MarkdownLinkRewriter.Rewrite("<a href=\"index.md\">home</a>"u8, true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"\">home</a>");
    }

    /// <summary>Link rewriter preserves anchors on directory-URL targets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryPreservesAnchors()
    {
        var html = "<a href=\"guide.md#install\">install</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"guide/#install\">install</a>");
    }

    /// <summary>Link rewriter preserves query strings on directory-URL targets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryPreservesQuery()
    {
        var html = "<a href=\"search.md?q=foo\">search</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"search/?q=foo\">search</a>");
    }

    /// <summary>Link rewriter passes external URLs through under both modes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterPassesExternalUrlsThrough()
    {
        var html = "<a href=\"https://example.com/foo.md\">e</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"https://example.com/foo.md\">e</a>");
    }

    /// <summary>Link rewriter collapses <c>index.md</c> with a fragment to <c>#section</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryIndexWithAnchor()
    {
        var rewritten = MarkdownLinkRewriter.Rewrite("<a href=\"index.md#top\">top</a>"u8, true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"#top\">top</a>");
    }

    /// <summary>DocBuilder fluent toggle is captured on the builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DocBuilderFluentEnablesDirectoryUrls()
    {
        DocBuilder builder = new();
        await Assert.That(builder.UseDirectoryUrlsEnabled).IsFalse();
        _ = builder.UseDirectoryUrls();
        await Assert.That(builder.UseDirectoryUrlsEnabled).IsTrue();
    }

    /// <summary>DocBuilder fluent overload accepts a literal toggle.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DocBuilderFluentOverloadAcceptsLiteralToggle()
    {
        var builder = new DocBuilder().UseDirectoryUrls(false);
        await Assert.That(builder.UseDirectoryUrlsEnabled).IsFalse();
    }
}
