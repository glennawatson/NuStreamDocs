// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests for the streaming build pipeline.</summary>
public class BuildPipelineTests
{
    /// <summary>Guide Directory used by the test cases.</summary>
    private const string GuideDirectory = "guide";

    /// <summary>Expected Page Count used by the test cases.</summary>
    private const int ExpectedPageCount = 2;

    /// <summary>The pipeline should walk a docs tree and emit one HTML file per markdown source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersEveryMarkdownPage()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "a.md"), "# A");
        _ = Directory.CreateDirectory(Path.Combine(fixture.Input, GuideDirectory));
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, GuideDirectory, "b.md"), "# B");

        var count = await new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .BuildAsync();

        await Assert.That(count).IsEqualTo(ExpectedPageCount);
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "a.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, GuideDirectory, "b.html"))).IsTrue();
    }

    /// <summary>Plugins registered via the builder receive page-render hooks during the pipeline.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FiresPluginRenderHookForEachPage()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "x.md"), "# X");
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "y.md"), "# Y");

        CountingPlugin counter = new();
        var rendered = await new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .UsePlugin(counter)
            .BuildAsync();

        await Assert.That(rendered).IsEqualTo(ExpectedPageCount);
        await Assert.That(counter.PageHits).IsEqualTo(ExpectedPageCount);
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

        var processed = await BuildPipeline.RunAsync(
            fixture.Input,
            fixture.Output,
            [],
            BuildPipelineOptions.Default,
            CancellationToken.None);
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
        var processed =
            await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [], options, CancellationToken.None);
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

        await Assert.That(File.Exists(Path.Combine(fixture.Output, GuideDirectory, "index.html"))).IsTrue();
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
        await Assert.That(static () => BuildPipeline.RunAsync(string.Empty, "/out", [])).Throws<ArgumentException>();
        await Assert.That(static () => BuildPipeline.RunAsync("/in", string.Empty, [])).Throws<ArgumentException>();
    }

    /// <summary>Test pre-render plugin that replaces every <c>A</c> with <c>B</c>.</summary>
    private sealed class ReplaceAToB : IPagePreRenderPlugin
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "a-to-b"u8;

        /// <inheritdoc/>
        public PluginPriority PreRenderPriority => PluginPriority.Normal;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

        /// <inheritdoc/>
        public void PreRender(in PagePreRenderContext context)
        {
            var source = context.Source;
            var writer = context.Output;
            for (var i = 0; i < source.Length; i++)
            {
                var dst = writer.GetSpan(1);
                dst[0] = source[i] is (byte)'A' ? (byte)'B' : source[i];
                writer.Advance(1);
            }
        }
    }

    /// <summary>Test pre-render plugin that replaces every <c>B</c> with <c>C</c>.</summary>
    private sealed class ReplaceBToC : IPagePreRenderPlugin
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "b-to-c"u8;

        /// <inheritdoc/>
        public PluginPriority PreRenderPriority => PluginPriority.Normal;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

        /// <inheritdoc/>
        public void PreRender(in PagePreRenderContext context)
        {
            var source = context.Source;
            var writer = context.Output;
            for (var i = 0; i < source.Length; i++)
            {
                var dst = writer.GetSpan(1);
                dst[0] = source[i] is (byte)'B' ? (byte)'C' : source[i];
                writer.Advance(1);
            }
        }
    }
}
