// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Layouts.Tests;

/// <summary>End-to-end tests for <see cref="LayoutsPlugin"/> via the post-render hook.</summary>
public class LayoutsPluginTests
{
    /// <summary>A page without a <c>template:</c> frontmatter key passes the rendered HTML through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoTemplate_PassesThrough()
    {
        using var fixture = new LayoutFixture();
        const string Source = "# Hello\n";
        const string Html = "<h1>Hello</h1>";
        var output = fixture.Run(Source, Html);
        await Assert.That(output).IsEqualTo(Html);
    }

    /// <summary>A page that names a missing layout file logs a warning and falls back to the rendered HTML.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingTemplate_FallsBackToHtml()
    {
        using var fixture = new LayoutFixture();
        const string Source = "---\ntemplate: nope.html\n---\n";
        const string Html = "<p>Body</p>";
        var output = fixture.Run(Source, Html);
        await Assert.That(output).IsEqualTo(Html);
    }

    /// <summary><c>{{ page.content }}</c> in the layout is replaced with the rendered HTML.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PageContent_Substituted()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("page.html", "<main>{{ page.content }}</main>");
        const string Source = "---\ntemplate: page.html\n---\n";
        const string Html = "<h1>Body</h1>";
        var output = fixture.Run(Source, Html);
        await Assert.That(output).IsEqualTo("<main><h1>Body</h1></main>");
    }

    /// <summary><c>{{ page.title }}</c> reads from the <c>title:</c> frontmatter key.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PageTitle_Substituted()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("page.html", "<title>{{ page.title }}</title>");
        const string Source = "---\ntemplate: page.html\ntitle: Hello\n---\n";
        var output = fixture.Run(Source, string.Empty);
        await Assert.That(output).IsEqualTo("<title>Hello</title>");
    }

    /// <summary>Arbitrary frontmatter scalar keys are exposed under <c>page.X</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FrontmatterScalar_Substituted()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("page.html", "<sub>{{ page.subtitle }}</sub>");
        const string Source = "---\ntemplate: page.html\nsubtitle: hello world\n---\n";
        var output = fixture.Run(Source, string.Empty);
        await Assert.That(output).IsEqualTo("<sub>hello world</sub>");
    }

    /// <summary><c>{% include %}</c> splices another file at that point.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Include_Splices()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("header.html", "<header>Site</header>");
        fixture.WriteTemplate("page.html", "{% include \"header.html\" %}<main>{{ page.content }}</main>");
        const string Source = "---\ntemplate: page.html\n---\n";
        const string Html = "<p>x</p>";
        var output = fixture.Run(Source, Html);
        await Assert.That(output).IsEqualTo("<header>Site</header><main><p>x</p></main>");
    }

    /// <summary>Recursive <c>{% include %}</c> stops at the configured depth cap.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncludeDepth_Capped()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("loop.html", "L{% include \"loop.html\" %}");
        fixture.WriteTemplate("page.html", "{% include \"loop.html\" %}");
        const string Source = "---\ntemplate: page.html\n---\n";
        var output = fixture.Run(Source, string.Empty, opts => opts.WithMaxIncludeDepth(3));

        // Initial include + 2 nested expansions before the cap kicks in.
        await Assert.That(output).IsEqualTo("LLL");
    }

    /// <summary><c>{% extends %}</c> + <c>{% block %}</c> overrides the parent block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExtendsBlock_Overrides()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("base.html", "<html><body>{% block body %}DEFAULT{% endblock %}</body></html>");
        fixture.WriteTemplate("page.html", "{% extends \"base.html\" %}{% block body %}CHILD{% endblock %}");
        const string Source = "---\ntemplate: page.html\n---\n";
        var output = fixture.Run(Source, string.Empty);
        await Assert.That(output).IsEqualTo("<html><body>CHILD</body></html>");
    }

    /// <summary>Unmatched parent blocks render their own default content.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExtendsBlock_UnmatchedRendersParentDefault()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("base.html", "[{% block a %}A{% endblock %}|{% block b %}B{% endblock %}]");
        fixture.WriteTemplate("page.html", "{% extends \"base.html\" %}{% block a %}AA{% endblock %}");
        const string Source = "---\ntemplate: page.html\n---\n";
        var output = fixture.Run(Source, string.Empty);
        await Assert.That(output).IsEqualTo("[AA|B]");
    }

    /// <summary><c>{{ super() }}</c> inside a child block emits the parent block's default content.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Super_EmitsParentContent()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("base.html", "{% block body %}PARENT{% endblock %}");
        fixture.WriteTemplate("page.html", "{% extends \"base.html\" %}{% block body %}[{{ super() }}]{% endblock %}");
        const string Source = "---\ntemplate: page.html\n---\n";
        var output = fixture.Run(Source, string.Empty);
        await Assert.That(output).IsEqualTo("[PARENT]");
    }

    /// <summary>Unsupported tags log a warning and pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnsupportedTag_PassesThrough()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("page.html", "[{% if x %}A{% endif %}]");
        const string Source = "---\ntemplate: page.html\n---\n";
        var output = fixture.Run(Source, string.Empty);
        await Assert.That(output).IsEqualTo("[{% if x %}A{% endif %}]");
    }

    /// <summary>Within a single build, the same template parses once even when many pages reference it.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Cache_SameBuild_TemplateParsedOnce()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("page.html", "<main>{{ page.content }}</main>");
        const string Source = "---\ntemplate: page.html\n---\n";
        var opts = LayoutsOptions.Default.WithTemplateDirectory(fixture.Root);
        LayoutsPlugin plugin = new(opts);

        for (var i = 0; i < 5; i++)
        {
            LayoutFixture.RunWith(plugin, Source, $"<p>{i}</p>");
        }

        // 5 renders, one cached template entry.
        await Assert.That(LayoutFixture.GetCacheCount(plugin)).IsEqualTo(1);
    }

    /// <summary>Across builds (simulating serve-mode rebuilds) <see cref="LayoutsPlugin.ConfigureAsync"/> drops the cache so on-disk template edits take effect.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Cache_RebuildAfterConfigure_PicksUpDiskChanges()
    {
        using var fixture = new LayoutFixture();
        fixture.WriteTemplate("page.html", "<main>FIRST {{ page.content }}</main>");
        const string Source = "---\ntemplate: page.html\n---\n";
        var opts = LayoutsOptions.Default.WithTemplateDirectory(fixture.Root);
        LayoutsPlugin plugin = new(opts);

        // First build.
        BuildConfigureContext ctx = new(default, default, [], new());
        await plugin.ConfigureAsync(ctx, CancellationToken.None);
        var firstOutput = LayoutFixture.RunWith(plugin, Source, "<p>x</p>");
        await Assert.That(firstOutput).IsEqualTo("<main>FIRST <p>x</p></main>");

        // Edit the template on disk (simulating a serve-mode file change).
        fixture.WriteTemplate("page.html", "<main>SECOND {{ page.content }}</main>");

        // Second build: ConfigureAsync clears the cache so the new bytes are picked up.
        await plugin.ConfigureAsync(ctx, CancellationToken.None);
        var secondOutput = LayoutFixture.RunWith(plugin, Source, "<p>y</p>");
        await Assert.That(secondOutput).IsEqualTo("<main>SECOND <p>y</p></main>");
    }

    /// <summary>Regression guard for the priority ordering — Layouts must run before any theme-shell plugin at <c>Latest, 0</c>.</summary>
    /// <remarks>
    /// An earlier version put Layouts at tiebreak <c>10</c>, which made it run after the theme and
    /// overwrite the full themed page with raw layout content (no chrome, no nav, no header).
    /// </remarks>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PostRenderPriority_RunsBeforeThemeShell()
    {
        var layouts = new LayoutsPlugin().PostRenderPriority;
        PluginPriority themeShell = new(PluginBand.Latest);
        await Assert.That(layouts.Band).IsEqualTo(PluginBand.Latest);
        await Assert.That(layouts < themeShell).IsTrue();
        await Assert.That(layouts.Tiebreak < 0).IsTrue();
    }

    /// <summary>Helper that wires up a temp template directory + plugin and exposes a one-shot run.</summary>
    private sealed class LayoutFixture : IDisposable
    {
        /// <summary>Root directory layouts are written to.</summary>
        private readonly string _root = Directory.CreateTempSubdirectory("layouts-tests").FullName;

        /// <summary>Gets the template root.</summary>
        public DirectoryPath Root => _root;

        /// <summary>Runs <paramref name="plugin"/> against a fresh page without re-creating the plugin (so the per-plugin cache survives between calls).</summary>
        /// <param name="plugin">Plugin instance.</param>
        /// <param name="source">UTF-8 markdown source.</param>
        /// <param name="html">Pre-rendered HTML body.</param>
        /// <returns>Rewritten HTML.</returns>
        public static string RunWith(LayoutsPlugin plugin, string source, string html)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            ArrayBufferWriter<byte> sink = new(256);
            PagePostRenderContext ctx = new(
                "p.md",
                Encoding.UTF8.GetBytes(source),
                Encoding.UTF8.GetBytes(html),
                sink);
            plugin.PostRender(in ctx);
            return Encoding.UTF8.GetString(sink.WrittenSpan);
        }

        /// <summary>Returns the current cache size for <paramref name="plugin"/>.</summary>
        /// <param name="plugin">Plugin instance.</param>
        /// <returns>Cached entry count.</returns>
        public static int GetCacheCount(LayoutsPlugin plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            return plugin.GetCacheForTests().Count;
        }

        /// <summary>Writes a template file to the temp root.</summary>
        /// <param name="name">File name (e.g. <c>page.html</c>).</param>
        /// <param name="content">Template body.</param>
        public void WriteTemplate(string name, string content) =>
            File.WriteAllBytes(Path.Combine(_root, name), Encoding.UTF8.GetBytes(content));

        /// <summary>Runs the plugin with default options.</summary>
        /// <param name="source">UTF-8 markdown source (frontmatter + body).</param>
        /// <param name="html">Pre-rendered HTML body.</param>
        /// <returns>Rewritten HTML.</returns>
        public string Run(string source, string html) =>
            Run(source, html, opts => opts);

        /// <summary>Runs the plugin with caller-customized options.</summary>
        /// <param name="source">UTF-8 markdown source.</param>
        /// <param name="html">Pre-rendered HTML body.</param>
        /// <param name="configure">Options customization (the template directory is pre-set).</param>
        /// <returns>Rewritten HTML.</returns>
        public string Run(string source, string html, Func<LayoutsOptions, LayoutsOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var opts = configure(LayoutsOptions.Default.WithTemplateDirectory(Root));
            LayoutsPlugin plugin = new(opts);
            ArrayBufferWriter<byte> sink = new(256);
            PagePostRenderContext ctx = new(
                "p.md",
                Encoding.UTF8.GetBytes(source),
                Encoding.UTF8.GetBytes(html),
                sink);
            plugin.PostRender(in ctx);
            return Encoding.UTF8.GetString(sink.WrittenSpan);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!Directory.Exists(_root))
            {
                return;
            }

            Directory.Delete(_root, true);
        }
    }
}
