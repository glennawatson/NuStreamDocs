// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
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

    /// <summary>Source filename for the directory landing page.</summary>
    private const string IndexFileName = "index.md";

    /// <summary>Mixed-case source filename for a directory landing page.</summary>
    private const string MixedCaseIndexFileName = "Index.md";

    /// <summary>Output filename for a directory landing page.</summary>
    private const string IndexOutputFileName = "index.html";

    /// <summary>Authored index content used to verify source preservation.</summary>
    private const string NamespaceMarkdown = "# Namespace";

    /// <summary>Markdown excluded from builds that omit drafts.</summary>
    private const string DraftMarkdown = "---\ndraft: true\n---\n# Draft";

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
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "draft.md"), DraftMarkdown);

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
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "draft.md"), DraftMarkdown);

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

        await Assert.That(File.Exists(Path.Combine(fixture.Output, GuideDirectory, IndexOutputFileName))).IsTrue();
    }

    /// <summary>Index sources that differ only by filename casing produce one page and an actionable warning.</summary>
    /// <param name="directory">The source directory containing both pages.</param>
    /// <param name="fileName">The second index filename.</param>
    /// <param name="useDirectoryUrls">Whether to emit directory URLs.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task WarnsAndSkipsIndexFilenameCollisions(
        [Matrix("", "api/System")] string directory,
        [Matrix(MixedCaseIndexFileName, "INDEX.md")] string fileName,
        [Matrix(false, true)] bool useDirectoryUrls,
        CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        var sourceDirectory = Path.Combine(fixture.Input, directory);
        _ = Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, IndexFileName), NamespaceMarkdown, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, fileName), "# Type", cancellationToken);
        if (Directory.GetFiles(sourceDirectory, "*.md").Length is 1)
        {
            Skip.Test("Two index filename casings require a case-sensitive source filesystem.");
        }

        var logger = new WarningLogger();
        var options = BuildPipelineOptions.Default with { UseDirectoryUrls = useDirectoryUrls, Logger = logger };
        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [], options, cancellationToken);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(Directory.GetFiles(fixture.Output, "*.html", SearchOption.AllDirectories).Length).IsEqualTo(1);
        await Assert.That(logger.Messages.Count).IsEqualTo(1);
        await Assert.That(logger.Messages[0]).Contains(Path.Combine(directory, IndexFileName).Replace('\\', '/'));
        await Assert.That(logger.Messages[0]).Contains(Path.Combine(directory, fileName).Replace('\\', '/'));
        await Assert.That(logger.Messages[0]).Contains("Rename");
    }

    /// <summary>A generated index owns the output while the ignored source file remains unchanged.</summary>
    /// <param name="useDirectoryUrls">Whether to emit directory URLs.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task WarnsAndUsesGeneratedIndexWhenSourceExists(bool useDirectoryUrls, CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, IndexFileName), NamespaceMarkdown, cancellationToken);
        var logger = new WarningLogger();
        var options = BuildPipelineOptions.Default with { UseDirectoryUrls = useDirectoryUrls, Logger = logger };
        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [new IndexPagePlugin()], options, cancellationToken);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(logger.Messages.Count).IsEqualTo(1);
        await Assert.That(logger.Messages[0]).Contains(IndexFileName);
        await Assert.That(logger.Messages[0]).Contains(MixedCaseIndexFileName);
        await Assert.That(logger.Messages[0]).Contains("generated");
        await Assert.That(logger.Messages[0]).Contains("ignored");
        var outputFileName = useDirectoryUrls ? IndexOutputFileName : "Index.html";
        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Output, outputFileName), cancellationToken);
        await Assert.That(html).Contains("Generated type");
        await Assert.That(html).DoesNotContain("Namespace");
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Input, IndexFileName), cancellationToken)).IsEqualTo(NamespaceMarkdown);
    }

    /// <summary>Each source directory can supply its own index page across repeated builds.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IndexPagesInSeparateDirectoriesSurviveCachedBuilds(CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        _ = Directory.CreateDirectory(Path.Combine(fixture.Input, GuideDirectory));
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, MixedCaseIndexFileName), "# Home", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, GuideDirectory, "INDEX.md"), "# Guide", cancellationToken);
        var options = BuildPipelineOptions.Default with { UseDirectoryUrls = true };

        var first = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [], options, cancellationToken);
        var second = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [], options, cancellationToken);

        await Assert.That(first).IsEqualTo(ExpectedPageCount);
        await Assert.That(second).IsEqualTo(ExpectedPageCount);
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Output, IndexOutputFileName), cancellationToken)).Contains("Home");
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Output, GuideDirectory, IndexOutputFileName), cancellationToken)).Contains("Guide");
    }

    /// <summary>An excluded draft index does not reserve the directory's landing-page output.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DraftIndexDoesNotConflictWithPublishedIndex(CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, IndexFileName), DraftMarkdown, cancellationToken);
        var options = BuildPipelineOptions.Default with { UseDirectoryUrls = true };

        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [new IndexPagePlugin()], options, cancellationToken);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Output, IndexOutputFileName), cancellationToken)).Contains("Generated type");
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

    /// <summary>Registers a generated index page.</summary>
    private sealed class IndexPagePlugin : IBuildDiscoverPlugin
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "index-page"u8;

        /// <inheritdoc/>
        public PluginPriority DiscoverPriority => PluginPriority.Normal;

        /// <inheritdoc/>
        public ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
        {
            context.SyntheticPages.Add(MixedCaseIndexFileName, [.. "# Generated type"u8]);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Collects warnings produced during page discovery.</summary>
    private sealed class WarningLogger : ILogger
    {
        /// <summary>Gets warning messages.</summary>
        public List<string> Messages { get; } = [];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => logLevel is LogLevel.Warning;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel is LogLevel.Warning)
            {
                Messages.Add(formatter(state, exception));
            }
        }
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
