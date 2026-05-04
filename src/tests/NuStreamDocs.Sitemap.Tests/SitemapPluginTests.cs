// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Sitemap.Tests;

/// <summary>Lifecycle tests for <c>SitemapPlugin</c>, <c>NotFoundPlugin</c>, and <c>RedirectsPlugin</c> registrations.</summary>
public class SitemapPluginTests
{
    /// <summary>End-to-end finalize emits sitemap.xml + robots.txt when SiteUrl is configured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SitemapEmitsBothFiles()
    {
        using var temp = new SitemapTempDir();
        var plugin = new SitemapPlugin();
        var ctx = new PluginConfigureContext("/in", temp.Root, []) { SiteUrl = Encoding.UTF8.GetBytes("https://docs.test") };
        await plugin.OnConfigureAsync(ctx, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(8);
        await plugin.OnRenderPageAsync(new("guide/intro.md", default, sink), CancellationToken.None);
        await plugin.OnRenderPageAsync(new("index.md", default, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(temp.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(temp.Root, "sitemap.xml"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(temp.Root, "robots.txt"))).IsTrue();
    }

    /// <summary>Without a SiteUrl the plugin no-ops at finalize.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SitemapNoOpsWithoutSiteUrl()
    {
        using var temp = new SitemapTempDir();
        var plugin = new SitemapPlugin();
        var ctx = new PluginConfigureContext("/in", temp.Root, []);
        await plugin.OnConfigureAsync(ctx, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(8);
        await plugin.OnRenderPageAsync(new("any.md", default, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(temp.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(temp.Root, "sitemap.xml"))).IsFalse();
    }

    /// <summary>Pages whose relative path produces an empty URL are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SitemapSkipsEmptyUrl()
    {
        using var temp = new SitemapTempDir();
        var plugin = new SitemapPlugin();
        var ctx = new PluginConfigureContext("/in", temp.Root, []) { SiteUrl = Encoding.UTF8.GetBytes("https://docs.test/") };
        await plugin.OnConfigureAsync(ctx, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(8);
        await plugin.OnRenderPageAsync(new(string.Empty, default, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(temp.Root), CancellationToken.None);

        // No entries → no sitemap written.
        await Assert.That(File.Exists(Path.Combine(temp.Root, "sitemap.xml"))).IsFalse();
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new SitemapPlugin().Name.AsSpan().SequenceEqual("sitemap"u8)).IsTrue();

    /// <summary>UseSitemap registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSitemapRegisters() =>
        await Assert.That(new DocBuilder().UseSitemap()).IsTypeOf<DocBuilder>();

    /// <summary>UseSitemap rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSitemapRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderSitemapExtensions.UseSitemap(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseNotFoundPage registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseNotFoundPageRegisters() =>
        await Assert.That(new DocBuilder().UseNotFoundPage()).IsTypeOf<DocBuilder>();

    /// <summary>UseNotFoundPage rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseNotFoundPageRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderSitemapExtensions.UseNotFoundPage(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseRedirects with entries registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseRedirectsWithEntries() =>
        await Assert.That(new DocBuilder().UseRedirects(("old.html", "/new.html"))).IsTypeOf<DocBuilder>();

    /// <summary>UseRedirects with options + entries registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseRedirectsWithOptions() =>
        await Assert.That(new DocBuilder().UseRedirects(RedirectsOptions.Default, ("old.html", "/new.html")))
            .IsTypeOf<DocBuilder>();

    /// <summary>UseRedirects rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseRedirectsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderSitemapExtensions.UseRedirects(null!, ("a.html", "/b.html")));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseRedirects(options) rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseRedirectsOptionsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderSitemapExtensions.UseRedirects(null!, RedirectsOptions.Default));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class SitemapTempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="SitemapTempDir"/> class.</summary>
        public SitemapTempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-sm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch directory.</summary>
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
                // already gone
            }
        }
    }
}
