// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;
using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>ExtraAssetsPlugin</c>.</summary>
public class ExtraAssetsTests
{
    /// <summary>Repeated <c>AddExtraCss</c> calls fold onto a single plugin instance.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FoldsRepeatedAddCallsOntoOnePlugin()
    {
        var fixture = TempDir.Create();
        try
        {
            var builder = new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .AddExtraCssInline("one.css", [.. "a { color: red }"u8])
                .AddExtraCssInline("two.css", [.. "b { color: blue }"u8]);

            // Trigger configure so head fragment + asset list materialize.
            await builder.BuildAsync();

            var plugin = ExtractPlugin(builder);
            var assetPaths = plugin.StaticAssets.Select(static p => p.Path).ToList();
            await Assert.That(assetPaths).Contains("assets/extra/one.css");
            await Assert.That(assetPaths).Contains("assets/extra/two.css");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    /// <summary>Inline CSS sources ship as static assets under <c>assets/extra/</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InlineCssIsExposedAsStaticAsset()
    {
        var plugin = new ExtraAssetsPlugin();
        plugin.AddCss(ExtraAssetSource.Inline("brand.css", [.. "body{}"u8]));
        var context = new BuildConfigureContext("/in", "/out", [], new());
        await plugin.ConfigureAsync(context, CancellationToken.None);

        var assets = plugin.StaticAssets;
        await Assert.That(assets).HasSingleItem();
        await Assert.That(assets[0].Path).IsEqualTo("assets/extra/brand.css");
    }

    /// <summary>Head fragments are emitted in registration order: CSS first, then JS.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HeadFragmentEmitsLinkAndScript()
    {
        var plugin = new ExtraAssetsPlugin();
        plugin.AddCss(ExtraAssetSource.Inline("a.css", [.. "x"u8]));
        plugin.AddJs(ExtraAssetSource.External("https://cdn.example/x.js"));
        var context = new BuildConfigureContext("/in", "/out", [], new());
        await plugin.ConfigureAsync(context, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>();
        plugin.WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(head).Contains("<link rel=\"stylesheet\" href=\"/assets/extra/a.css\">");
        await Assert.That(head).Contains("<script src=\"https://cdn.example/x.js\" defer></script>");
        var linkIndex = head.IndexOf("<link", StringComparison.Ordinal);
        var scriptIndex = head.IndexOf("<script", StringComparison.Ordinal);
        await Assert.That(linkIndex).IsLessThan(scriptIndex);
    }

    /// <summary>External URL sources emit a tag without shipping any asset.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExternalUrlShipsNoAsset()
    {
        var plugin = new ExtraAssetsPlugin();
        plugin.AddCss(ExtraAssetSource.External("https://cdn.example/x.css"));
        var context = new BuildConfigureContext("/in", "/out", [], new());
        await plugin.ConfigureAsync(context, CancellationToken.None);

        await Assert.That(plugin.StaticAssets).IsEmpty();
    }

    /// <summary>WriteHeadExtra is a no-op when no sources are registered.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyHeadFragmentIsNoOp()
    {
        var plugin = new ExtraAssetsPlugin();
        await plugin.ConfigureAsync(default, CancellationToken.None);
        var sink = new ArrayBufferWriter<byte>();
        plugin.WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>External URL CSS / JS sources do not contribute to StaticAssets but appear in head.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExternalUrlSkipsStaticAssetEmit()
    {
        var plugin = new ExtraAssetsPlugin();
        plugin.AddCss(ExtraAssetSource.External("https://cdn.test/x.css"));
        plugin.AddJs(ExtraAssetSource.External("https://cdn.test/x.js"));
        await plugin.ConfigureAsync(default, CancellationToken.None);

        await Assert.That(plugin.StaticAssets.Length).IsEqualTo(0);

        var sink = new ArrayBufferWriter<byte>();
        plugin.WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("https://cdn.test/x.css");
        await Assert.That(head).Contains("https://cdn.test/x.js");
    }

    /// <summary>File-kind CSS sources read disk bytes and ship as static assets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FileKindReadsBytesAndShips()
    {
        using var dir = TempDir.Create();
        var path = Path.Combine(dir.Root, "extra.css");
        await File.WriteAllTextAsync(path, "body { color: red; }");

        var plugin = new ExtraAssetsPlugin();
        plugin.AddCss(ExtraAssetSource.File(path));
        await plugin.ConfigureAsync(default, CancellationToken.None);

        await Assert.That(plugin.StaticAssets.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(plugin.StaticAssets[0].Bytes)).Contains("color: red");
    }

    /// <summary>Inline-kind sources ship their bytes verbatim and head emits a defer-script tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineKindShipsBytesAndEmitsScript()
    {
        var plugin = new ExtraAssetsPlugin();
        plugin.AddJs(ExtraAssetSource.Inline("inline.js", "console.log('hi')"u8.ToArray()));
        await plugin.ConfigureAsync(default, CancellationToken.None);

        await Assert.That(plugin.StaticAssets.Length).IsEqualTo(1);
        await Assert.That(plugin.StaticAssets[0].Path).IsEqualTo("assets/extra/inline.js");

        var sink = new ArrayBufferWriter<byte>();
        plugin.WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("/assets/extra/inline.js");
        await Assert.That(head).Contains("defer");
    }

    /// <summary>AddCss/AddJs reject null sources.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddRejectsNull()
    {
        var plugin = new ExtraAssetsPlugin();
        await Assert.That(() => plugin.AddCss(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => plugin.AddJs(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>WriteHeadExtra rejects a null writer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraNullWriterThrows() =>
        await Assert.That(() => new ExtraAssetsPlugin().WriteHeadExtra(null!)).Throws<ArgumentNullException>();

    /// <summary>Pulls the singleton <c>ExtraAssetsPlugin</c> back out of the builder via the public extension.</summary>
    /// <param name="builder">Builder.</param>
    /// <returns>The folded plugin instance.</returns>
    private static ExtraAssetsPlugin ExtractPlugin(DocBuilder builder) =>
        builder.GetOrAddPlugin<ExtraAssetsPlugin>();

    /// <summary>Disposable temp directory fixture used by the build-driving tests in this file.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        /// <param name="root">Absolute path to the temp root.</param>
        private TempDir(string root)
        {
            Root = root;
            Output = Path.Combine(root, "_site");
        }

        /// <summary>Gets the absolute path to the temp root.</summary>
        public string Root { get; }

        /// <summary>Gets the absolute path to the output directory.</summary>
        public string Output { get; }

        /// <summary>Creates a fresh temp directory under <c>Path.GetTempPath</c>.</summary>
        /// <returns>A new fixture; caller must dispose.</returns>
        public static TempDir Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "smkd-extras-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(root);
            return new(root);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }
}
