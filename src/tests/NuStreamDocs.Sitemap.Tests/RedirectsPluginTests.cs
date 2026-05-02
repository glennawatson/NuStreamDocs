// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Config;

namespace NuStreamDocs.Sitemap.Tests;

/// <summary>Behavior tests for <c>RedirectsPlugin</c> covering config file + frontmatter alias paths.</summary>
public class RedirectsPluginTests
{
    /// <summary>A redirects.yml file is loaded and merged into the redirect map.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoadsConfigFile()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();
        var configPath = Path.Combine(input.Root, "redirects.yml");
        const string ConfigBody = "old.html: /new.html\nlegacy/page.html: \"/guide/intro.html\"\n";
        await File.WriteAllTextAsync(configPath, ConfigBody);

        var plugin = new RedirectsPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(output.Root, "old.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output.Root, "legacy", "page.html"))).IsTrue();
    }

    /// <summary>Per-page <c>aliases:</c> frontmatter lists become redirect stubs targeting the page.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FrontmatterAliasesEmitStubs()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();

        var plugin = new RedirectsPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);

        const string Source = "---\naliases:\n  - old/page\n  - really/old\n---\nbody";
        var bytes = System.Text.Encoding.UTF8.GetBytes(Source);
        var sink = new ArrayBufferWriter<byte>(64);
        await plugin.OnRenderPageAsync(new("guide/intro.md", bytes, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        var aliasHtml = await File.ReadAllTextAsync(Path.Combine(output.Root, "old", "page.html"));
        await Assert.That(aliasHtml).Contains("guide/intro.html");
    }

    /// <summary>Inline list aliases (<c>[a, b]</c>) work too.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineListAliasesEmitStubs()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();

        var plugin = new RedirectsPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);

        const string Source = "---\naliases: [\"old.html\", \"prev.html\"]\n---\nbody";
        var bytes = System.Text.Encoding.UTF8.GetBytes(Source);
        var sink = new ArrayBufferWriter<byte>(64);
        await plugin.OnRenderPageAsync(new("page.md", bytes, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(output.Root, "old.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output.Root, "prev.html"))).IsTrue();
    }

    /// <summary>Aliases ending in <c>/</c> are normalized to <c>index.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingSlashAliasNormalizesToIndex()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();

        var plugin = new RedirectsPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);

        const string Source = "---\naliases:\n  - section/\n---\nbody";
        var bytes = System.Text.Encoding.UTF8.GetBytes(Source);
        var sink = new ArrayBufferWriter<byte>(64);
        await plugin.OnRenderPageAsync(new("page.md", bytes, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(output.Root, "section", "index.html"))).IsTrue();
    }

    /// <summary>Disabling alias scanning skips frontmatter aliases entirely.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScanFrontmatterAliasesDisabled()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();

        var options = RedirectsOptions.Default with { ScanFrontmatterAliases = false, LoadConfigFile = false };
        var plugin = new RedirectsPlugin(options, []);
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);

        const string Source = "---\naliases:\n  - skipped.html\n---\nbody";
        var bytes = System.Text.Encoding.UTF8.GetBytes(Source);
        var sink = new ArrayBufferWriter<byte>(64);
        await plugin.OnRenderPageAsync(new("page.md", bytes, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(output.Root, "skipped.html"))).IsFalse();
    }

    /// <summary>Static seed entries win over alias entries on conflict.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StaticEntriesWinOverAliases()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();

        var plugin = new RedirectsPlugin(("old.html", "/wins.html"));
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);

        const string Source = "---\naliases:\n  - old.html\n---\nbody";
        var bytes = System.Text.Encoding.UTF8.GetBytes(Source);
        var sink = new ArrayBufferWriter<byte>(64);
        await plugin.OnRenderPageAsync(new("loses.md", bytes, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        var html = await File.ReadAllTextAsync(Path.Combine(output.Root, "old.html"));
        await Assert.That(html).Contains("/wins.html");
    }

    /// <summary>Pages without frontmatter at all simply skip the alias scan.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoFrontmatterIsHarmless()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();

        var plugin = new RedirectsPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(8);
        await plugin.OnRenderPageAsync(new("page.md", System.Text.Encoding.UTF8.GetBytes("just body"), sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        // No stubs written.
        await Assert.That(Directory.GetFiles(output.Root)).IsEmpty();
    }

    /// <summary>RedirectsPlugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new RedirectsPlugin().Name).IsEqualTo("redirects");

    /// <summary>Static seed entries with whitespace-only from/to are filtered out.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankSeedEntriesIgnored()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();
        var plugin = new RedirectsPlugin(("good.html", "/dest.html"), (string.Empty, "/x"), ("from", "  "), ("   ", "   "));
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(output.Root, "good.html"))).IsTrue();
        await Assert.That(Directory.GetFiles(output.Root).Length).IsEqualTo(1);
    }

    /// <summary>An empty merged map skips writing anything.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyMergedMapWritesNothing()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();
        var plugin = new RedirectsPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(Directory.GetFiles(output.Root)).IsEmpty();
    }

    /// <summary>LoadConfigFile=false bypasses the redirects.yml read even when the file exists.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoadConfigFileDisabled()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();
        await File.WriteAllTextAsync(Path.Combine(input.Root, "redirects.yml"), "old.html: /new.html\n");

        var plugin = new RedirectsPlugin(RedirectsOptions.Default with { LoadConfigFile = false }, []);
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(output.Root, "old.html"))).IsFalse();
    }

    /// <summary>An alias with no extension picks up <c>.html</c>; one already ending <c>.html</c> is left intact.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AliasExtensionNormalization()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();
        var plugin = new RedirectsPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);

        var source = "---\naliases: [bare, already.html, with-ext.HTML]\n---\nbody"u8.ToArray();
        var sink = new ArrayBufferWriter<byte>(8);
        await plugin.OnRenderPageAsync(new("page.md", source, sink), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(output.Root, "bare.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output.Root, "already.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output.Root, "with-ext.HTML"))).IsTrue();
    }

    /// <summary>The redirect stub HTML-attribute-escapes each special char in the destination URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StubAttributeEscapesEverySpecialChar()
    {
        using var input = new RedirectsTempDir();
        using var output = new RedirectsTempDir();
        var plugin = new RedirectsPlugin(("from.html", "/dst?a=1&b=\"2\"<3>"));
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, input.Root, output.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(output.Root), CancellationToken.None);

        var stub = await File.ReadAllTextAsync(Path.Combine(output.Root, "from.html"));
        await Assert.That(stub).Contains("&amp;");
        await Assert.That(stub).Contains("&quot;");
        await Assert.That(stub).Contains("&lt;");
        await Assert.That(stub).Contains("&gt;");
    }

    /// <summary>Constructor rejects a null entries array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullEntriesThrows() =>
        await Assert.That(() => new RedirectsPlugin(null!)).Throws<ArgumentNullException>();

    /// <summary>Disposable scratch directory.</summary>
    private sealed class RedirectsTempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="RedirectsTempDir"/> class.</summary>
        public RedirectsTempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-rd-" + Guid.NewGuid().ToString("N"));
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
