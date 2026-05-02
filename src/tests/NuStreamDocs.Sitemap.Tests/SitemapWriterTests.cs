// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Sitemap.Tests;

/// <summary>Behavior tests for <c>SitemapWriter</c> and <c>NotFoundPlugin</c>.</summary>
public class SitemapWriterTests
{
    /// <summary>Maps <c>foo.md</c> to <c>foo.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathSwapsExtension() =>
        await Assert.That(Encoding.UTF8.GetString(SitemapWriter.RelativePathToUrlPath("guide/intro.md")))
            .IsEqualTo("guide/intro.html");

    /// <summary>Backslashes are normalized to forward slashes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathNormalizesSeparators() =>
        await Assert.That(Encoding.UTF8.GetString(SitemapWriter.RelativePathToUrlPath("guide\\intro.md")))
            .IsEqualTo("guide/intro.html");

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(SitemapWriter.RelativePathToUrlPath(string.Empty).Length).IsEqualTo(0);

    /// <summary>Writing the sitemap produces the expected XML envelope and per-URL elements.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteSitemapEmitsXml()
    {
        using var fixture = new TempDirectory();
        SitemapWriter.WriteSitemap(
            fixture.Root,
            "https://docs.test/"u8.ToArray(),
            ["index.html"u8.ToArray(), "guide/intro.html"u8.ToArray()]);

        var xml = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "sitemap.xml"));
        await Assert.That(xml).Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        await Assert.That(xml).Contains("<loc>https://docs.test/index.html</loc>");
        await Assert.That(xml).Contains("<loc>https://docs.test/guide/intro.html</loc>");
        await Assert.That(xml).Contains("</urlset>");
    }

    /// <summary>Writing the robots file points crawlers at the sitemap URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteRobotsPointsAtSitemap()
    {
        using var fixture = new TempDirectory();
        SitemapWriter.WriteRobots(fixture.Root, "https://docs.test/"u8.ToArray());

        var txt = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "robots.txt"));
        await Assert.That(txt).Contains("User-agent: *");
        await Assert.That(txt).Contains("Sitemap: https://docs.test/sitemap.xml");
    }

    /// <summary><c>NotFoundPlugin</c> writes a default <c>404.html</c> when none exists.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NotFoundPluginWritesDefault()
    {
        using var fixture = new TempDirectory();
        var plugin = new NotFoundPlugin();
        await plugin.OnFinalizeAsync(new(fixture.Root), CancellationToken.None);

        var path = Path.Combine(fixture.Root, "404.html");
        await Assert.That(File.Exists(path)).IsTrue();
        var html = await File.ReadAllTextAsync(path);
        await Assert.That(html).Contains("<title>404 — Page not found</title>");
    }

    /// <summary><c>NotFoundPlugin</c> leaves an existing <c>404.html</c> untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NotFoundPluginKeepsExistingPage()
    {
        using var fixture = new TempDirectory();
        var path = Path.Combine(fixture.Root, "404.html");
        await File.WriteAllTextAsync(path, "user content");

        var plugin = new NotFoundPlugin();
        await plugin.OnFinalizeAsync(new(fixture.Root), CancellationToken.None);

        await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("user content");
    }

    /// <summary><c>RedirectsPlugin</c> writes a meta-refresh stub for each entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RedirectsPluginWritesStubs()
    {
        using var fixture = new TempDirectory();
        var plugin = new RedirectsPlugin(("old.html", "/new.html"), ("legacy/page.html", "/guide/intro.html"));
        await plugin.OnFinalizeAsync(new(fixture.Root), CancellationToken.None);

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "old.html"));
        await Assert.That(html).Contains("<meta http-equiv=\"refresh\" content=\"0; url=/new.html\">");
        await Assert.That(html).Contains("<link rel=\"canonical\" href=\"/new.html\">");

        var legacy = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "legacy", "page.html"));
        await Assert.That(legacy).Contains("url=/guide/intro.html");
    }

    /// <summary>Disposable scratch-directory helper.</summary>
    private sealed class TempDirectory : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TempDirectory"/> class.
        /// </summary>
        public TempDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-sitemap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute scratch-directory path.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // Already removed.
            }
        }
    }
}
