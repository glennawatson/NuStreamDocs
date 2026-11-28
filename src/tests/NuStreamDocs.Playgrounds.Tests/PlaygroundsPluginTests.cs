// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.SuperFences;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.Playgrounds.Tests;

/// <summary>Tests for <see cref="PlaygroundsPlugin"/>, HTTP request parsing, and sandbox emitters.</summary>
public sealed class PlaygroundsPluginTests
{
    /// <summary>Index HTML filename used in site output assertions.</summary>
    private const string IndexHtml = "index.html";

    /// <summary>Verifies option extensions and null guards across <see cref="PlaygroundsOptionsExtensions"/>, handlers, plugin, and builder extensions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GuardsAndOptions_InvalidAndValidInputs_BehaveAsExpected()
    {
        PlaygroundsOptions? nullOpts = null;
        DocBuilder? nullBuilder = null;

        await Assert.That(() => nullOpts!.WithInteractiveTryIt(false)).Throws<ArgumentNullException>();
        await Assert.That(() => nullOpts!.WithAssets(false)).Throws<ArgumentNullException>();
        await Assert.That(static () => new HttpRequestFenceHandler(null!)).Throws<ArgumentNullException>();
        await Assert.That(static () => new PlaygroundsPlugin(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UsePlaygrounds()).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UsePlaygrounds(PlaygroundsOptions.Default)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UsePlaygrounds((PlaygroundsOptions)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => nullBuilder!.UsePlaygrounds(static o => o)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UsePlaygrounds((Func<PlaygroundsOptions, PlaygroundsOptions>)null!)).Throws<ArgumentNullException>();

        var updated = PlaygroundsOptions.Default.WithInteractiveTryIt(false).WithAssets(false);
        await Assert.That(updated.EnableInteractiveTryIt).IsFalse();
        await Assert.That(updated.IncludeAssets).IsFalse();

        PlaygroundsPlugin defaultPlugin = new();
        await Assert.That(Encoding.UTF8.GetString(defaultPlugin.Name)).IsEqualTo("playgrounds");
        await Assert.That(Encoding.UTF8.GetString(new HttpRequestFenceHandler(PlaygroundsOptions.Default).Name)).IsEqualTo("playgrounds-http-fence");
        await Assert.That(Encoding.UTF8.GetString(new PlaygroundFenceHandler().Name)).IsEqualTo("playgrounds-sandbox-fence");
    }

    /// <summary>Verifies HTTP parser, entity decoding, HTML escaping, and multi-language snippet generation edge cases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HttpRequestParserAndEmitter_AllShapes_RenderExpectedSnippets()
    {
        var emptySpec = HttpRequestParser.Parse("   \r\n  "u8);
        await Assert.That(Encoding.UTF8.GetString(emptySpec.Method)).IsEqualTo("GET");
        await Assert.That(Encoding.UTF8.GetString(emptySpec.Url)).IsEqualTo("/");

        var urlOnlySpec = HttpRequestParser.Parse("https://api.example.com/health"u8);
        await Assert.That(Encoding.UTF8.GetString(urlOnlySpec.Method)).IsEqualTo("GET");
        await Assert.That(Encoding.UTF8.GetString(urlOnlySpec.Url)).IsEqualTo("https://api.example.com/health");

        var decoded = HttpRequestParser.DecodeHtmlEntities("&lt;a&gt; &amp; &quot;x&quot; &#39;y&#39; &unknown;"u8);
        await Assert.That(Encoding.UTF8.GetString(decoded)).IsEqualTo("<a> & \"x\" 'y' &unknown;");
        await Assert.That(Encoding.UTF8.GetString(HttpRequestParser.DecodeHtmlEntities("plain"u8))).IsEqualTo("plain");

        var postSpec = """
            post https://api.example.com/v1/items?a=1&b=2
            Authorization: Bearer secret
            X-Trace: <trace>"id"
            malformedHeaderWithoutColon
            :emptyHeaderName

            "{\"name\":\"Widget\"}"
            """u8;

        ArrayBufferWriter<byte> withTryIt = new();
        HttpRequestWidgetEmitter.Render(postSpec, true, withTryIt);
        var html = Encoding.UTF8.GetString(withTryIt.WrittenSpan);
        await Assert.That(html).Contains("data-method=\"POST\"");
        await Assert.That(html).Contains("curl -X POST");
        await Assert.That(html).Contains("HttpRequestMessage");
        await Assert.That(html).Contains("await fetch(");
        await Assert.That(html).Contains("nd-http-tryit");
        await Assert.That(html).Contains("&lt;trace&gt;&quot;id&quot;");

        ArrayBufferWriter<byte> getNoTryIt = new();
        HttpRequestWidgetEmitter.Render("GET https://api.example.com/ping"u8, false, getNoTryIt);
        var getHtml = Encoding.UTF8.GetString(getNoTryIt.WrittenSpan);
        await Assert.That(getHtml).DoesNotContain("nd-http-tryit");
        await Assert.That(getHtml).DoesNotContain("headers:");
    }

    /// <summary>Verifies all sandbox providers (<c>dotnetfiddle</c>, <c>stackblitz</c>, <c>codepen</c>, <c>jsfiddle</c>, custom <c>src</c>) and HTML rewriter branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CodeSandboxAndHtmlRewriter_AllProvidersAndOrdering_RenderCleanly()
    {
        ArrayBufferWriter<byte> stackBlitzOut = new();
        CodeSandboxWidgetEmitter.Render("provider=stackblitz\nid=demo-app\nignoredLine\n=emptyKey\nunknown=1"u8, stackBlitzOut);
        await Assert.That(Encoding.UTF8.GetString(stackBlitzOut.WrittenSpan)).Contains("https://stackblitz.com/edit/demo-app?embed=1");

        ArrayBufferWriter<byte> codePenOut = new();
        CodeSandboxWidgetEmitter.Render("provider=codepen\nid=pen1\ntitle=My Pen"u8, codePenOut);
        await Assert.That(Encoding.UTF8.GetString(codePenOut.WrittenSpan)).Contains("https://codepen.io/embed/pen1");
        await Assert.That(Encoding.UTF8.GetString(codePenOut.WrittenSpan)).Contains("title=\"My Pen\"");

        ArrayBufferWriter<byte> jsFiddleOut = new();
        CodeSandboxWidgetEmitter.Render("provider=jsfiddle\nid=fiddle1"u8, jsFiddleOut);
        await Assert.That(Encoding.UTF8.GetString(jsFiddleOut.WrittenSpan)).Contains("https://jsfiddle.net/fiddle1/embedded/");

        ArrayBufferWriter<byte> customOut = new();
        CodeSandboxWidgetEmitter.Render("src=https://custom.example.test/embed"u8, customOut);
        await Assert.That(Encoding.UTF8.GetString(customOut.WrittenSpan)).Contains("https://custom.example.test/embed");

        await Assert.That(PlaygroundsHtmlRewriter.NeedsRewrite("<p>none</p>"u8)).IsFalse();
        await Assert.That(PlaygroundsHtmlRewriter.NeedsRewrite("<pre><code class=\"language-playground\">id=1</code></pre>"u8)).IsTrue();

        ArrayBufferWriter<byte> onlySandbox = new();
        PlaygroundsHtmlRewriter.Rewrite("<pre><code class=\"language-playground\">id=only</code></pre><p>tail</p>"u8, true, onlySandbox);
        await Assert.That(Encoding.UTF8.GetString(onlySandbox.WrittenSpan)).Contains("<p>tail</p>");

        var mixedHtml = """
            <p>Lead</p>
            <pre><code class="language-playground">provider=dotnetfiddle
            id=abc</code></pre>
            <pre><code class="language-http-request">GET https://api.example.com/items</code></pre>
            <pre><code class="language-http-request">unclosed
            """u8;

        ArrayBufferWriter<byte> rewritten = new();
        PlaygroundsHtmlRewriter.Rewrite(mixedHtml, false, rewritten);
        var outHtml = Encoding.UTF8.GetString(rewritten.WrittenSpan);
        await Assert.That(outHtml).Contains("https://dotnetfiddle.net/Widget/abc");
        await Assert.That(outHtml).Contains("data-url=\"https://api.example.com/items\"");
        await Assert.That(outHtml).Contains("<pre><code class=\"language-http-request\">unclosed");
    }

    /// <summary>Verifies full <see cref="DocBuilder"/> builds both with and without <c>SuperFencesPlugin</c> and across asset toggle modes.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocBuilder_WithAndWithoutSuperFences_RendersPlaygroundsAndAssets(CancellationToken cancellationToken)
    {
        var dir = Directory.CreateTempSubdirectory("nstd-playgrounds-");
        try
        {
            var docs = Directory.CreateDirectory(Path.Combine(dir.FullName, "docs"));
            const string md = """
                # API Guide

                ```http-request
                GET https://api.example.com/v1/status
                ```

                ```playground
                provider=dotnetfiddle
                id=xyz987
                ```
                """;
            await File.WriteAllTextAsync(Path.Combine(docs.FullName, "index.md"), md, cancellationToken);

            var outSf = Path.Combine(dir.FullName, "site-sf");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outSf)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseSuperFences()
                .UsePlaygrounds()
                .BuildAsync(cancellationToken);

            var superFencesHtml = await File.ReadAllTextAsync(Path.Combine(outSf, IndexHtml), cancellationToken);
            await Assert.That(superFencesHtml).Contains("nd-http-playground");
            await Assert.That(superFencesHtml).Contains("dotnetfiddle.net/Widget/xyz987");
            await Assert.That(superFencesHtml).Contains("assets/javascripts/nstd-playgrounds.js");

            var outCssOnly = Path.Combine(dir.FullName, "site-css-only");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outCssOnly)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UsePlaygrounds(static o => o.WithInteractiveTryIt(false))
                .BuildAsync(cancellationToken);

            var cssOnlyHtml = await File.ReadAllTextAsync(Path.Combine(outCssOnly, IndexHtml), cancellationToken);
            await Assert.That(cssOnlyHtml).Contains("assets/stylesheets/nstd-playgrounds.css");
            await Assert.That(cssOnlyHtml).DoesNotContain("nstd-playgrounds.js");

            var outNoAssets = Path.Combine(dir.FullName, "site-no-assets");
            await new DocBuilder()
                .WithInput(docs.FullName)
                .WithOutput(outNoAssets)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UsePlaygrounds(static o => o.WithAssets(false))
                .BuildAsync(cancellationToken);

            var noAssetsHtml = await File.ReadAllTextAsync(Path.Combine(outNoAssets, IndexHtml), cancellationToken);
            await Assert.That(noAssetsHtml).DoesNotContain("nstd-playgrounds.css");
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
