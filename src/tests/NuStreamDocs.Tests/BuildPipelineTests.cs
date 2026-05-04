// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests for the streaming build pipeline.</summary>
public class BuildPipelineTests
{
    /// <summary>The pipeline should walk a docs tree and emit one HTML file per markdown source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersEveryMarkdownPage()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "a.md"), "# A");
        Directory.CreateDirectory(Path.Combine(fixture.Input, "guide"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "guide", "b.md"), "# B");

        var count = await new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .BuildAsync();

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "a.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "guide", "b.html"))).IsTrue();
    }

    /// <summary>Plugins registered via the builder receive page-render hooks during the pipeline.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FiresPluginRenderHookForEachPage()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "x.md"), "# X");
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "y.md"), "# Y");

        var counter = new CountingPlugin();
        var rendered = await new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .UsePlugin(counter)
            .BuildAsync();

        await Assert.That(rendered).IsEqualTo(2);
        await Assert.That(counter.PageHits).IsEqualTo(2);
        await Assert.That(counter.ConfigureHits).IsEqualTo(1);
        await Assert.That(counter.FinalizeHits).IsEqualTo(1);
    }

    /// <summary>Drafts are excluded by default.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DraftPagesExcludedByDefault()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "live.md"), "# Live");
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "draft.md"), "---\ndraft: true\n---\n# Draft");

        var processed = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [], BuildPipelineOptions.Default, CancellationToken.None);
        await Assert.That(processed).IsEqualTo(1);
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "live.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "draft.html"))).IsFalse();
    }

    /// <summary>IncludeDrafts=true builds the draft pages too.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncludeDraftsBuildsDraftPages()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "draft.md"), "---\ndraft: true\n---\n# Draft");

        var options = BuildPipelineOptions.Default with { IncludeDrafts = true };
        var processed = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [], options, CancellationToken.None);
        await Assert.That(processed).IsEqualTo(1);
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "draft.html"))).IsTrue();
    }

    /// <summary>UseDirectoryUrls switches output shape to <c>foo/index.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseDirectoryUrlsEmitsIndexHtml()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "guide.md"), "# Guide");

        var options = BuildPipelineOptions.Default with { UseDirectoryUrls = true };
        await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [], options, CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(fixture.Output, "guide", "index.html"))).IsTrue();
    }

    /// <summary>Re-running with the same source bytes produces a cache hit (manifest hash match).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SecondBuildHitsCache()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "page.md"), "# Page");

        var first = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, []);
        var second = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, []);
        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(1);
    }

    /// <summary>Multiple preprocessors thread the bytes through every one (ping-pong scratch buffers).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultiplePreprocessorsAreChained()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "page.md"), "AAA\n");

        var processed = await BuildPipeline.RunAsync(
            fixture.Input,
            fixture.Output,
            [new ReplaceAToB(), new ReplaceBToC()],
            BuildPipelineOptions.Default,
            CancellationToken.None);

        await Assert.That(processed).IsEqualTo(1);
        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "page.html"));
        await Assert.That(html).Contains("CCC");
    }

    /// <summary>Empty inputRoot or outputRoot or null plugins are rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RootArgValidation()
    {
        await Assert.That(() => BuildPipeline.RunAsync(string.Empty, "/out", [])).Throws<ArgumentException>();
        await Assert.That(() => BuildPipeline.RunAsync("/in", string.Empty, [])).Throws<ArgumentException>();
        await Assert.That(() => BuildPipeline.RunAsync("/in", "/out", null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Test preprocessor that replaces every <c>A</c> with <c>B</c>.</summary>
    private sealed class ReplaceAToB : IDocPlugin, IMarkdownPreprocessor
    {
        /// <inheritdoc/>
        public byte[] Name => "a-to-b"u8.ToArray();

        /// <inheritdoc/>
        public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

        /// <inheritdoc/>
        public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath) =>
            Preprocess(source, writer);

        /// <inheritdoc/>
        public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
        {
            for (var i = 0; i < source.Length; i++)
            {
                var dst = writer.GetSpan(1);
                dst[0] = source[i] is (byte)'A' ? (byte)'B' : source[i];
                writer.Advance(1);
            }
        }
    }

    /// <summary>Test preprocessor that replaces every <c>B</c> with <c>C</c>.</summary>
    private sealed class ReplaceBToC : IDocPlugin, IMarkdownPreprocessor
    {
        /// <inheritdoc/>
        public byte[] Name => "b-to-c"u8.ToArray();

        /// <inheritdoc/>
        public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
        {
            for (var i = 0; i < source.Length; i++)
            {
                var dst = writer.GetSpan(1);
                dst[0] = source[i] is (byte)'B' ? (byte)'C' : source[i];
                writer.Advance(1);
            }
        }

        /// <inheritdoc/>
        public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath) =>
            Preprocess(source, writer);

        /// <inheritdoc/>
        public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

        /// <inheritdoc/>
        public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
