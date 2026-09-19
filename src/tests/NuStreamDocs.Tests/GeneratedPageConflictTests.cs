// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Generated output takes precedence without modifying source Markdown.</summary>
public sealed class GeneratedPageConflictTests
{
    /// <summary>Authored content includes metadata that must remain intact on disk.</summary>
    private const string SourceMarkdown = "---\nTitle: Authored page\ncustom: keep\n---\n# Authored page";

    /// <summary>Content supplied by the page generator.</summary>
    private const string GeneratedMarkdown = "# Generated page";

    /// <summary>The generated heading shown in rendered output.</summary>
    private const string GeneratedHeading = "Generated page";

    /// <summary>Filename of the shared landing page.</summary>
    private const string IndexFileName = "index.md";

    /// <summary>Both cold and cached builds report the ignored source.</summary>
    private const int RepeatedBuildCount = 2;

    /// <summary>Distinct flat URLs retain both pages.</summary>
    private const int DistinctPageCount = 2;

    /// <summary>Gets the generated Markdown bytes.</summary>
    private static ReadOnlySpan<byte> GeneratedBytes => "# Generated page"u8;

    /// <summary>Gets draft content that must not reserve output when drafts are excluded.</summary>
    private static ReadOnlySpan<byte> DraftMarkdown => "---\ndraft: true\n---\n# Generated draft"u8;

    /// <summary>Generated pages win for both eager and streaming sources, including identical content.</summary>
    /// <param name="path">The source-relative page name.</param>
    /// <param name="stream">Whether generation uses an asynchronous stream.</param>
    /// <param name="identical">Whether source and generated Markdown are identical.</param>
    /// <param name="directoryUrls">Whether the output uses directory URLs.</param>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [MatrixDataSource]
    public async Task GeneratedPageWinsAndWarns(
        [Matrix("Announcements/index.md", "guide.md")] string path,
        [Matrix(false, true)] bool stream,
        [Matrix(false, true)] bool identical,
        [Matrix(false, true)] bool directoryUrls,
        CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        var sourcePath = Path.Combine(fixture.Input, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        var source = identical ? GeneratedMarkdown : SourceMarkdown;
        await File.WriteAllTextAsync(sourcePath, source, cancellationToken);
        var logger = new WarningLogger();
        var options = BuildPipelineOptions.Default with { Logger = logger, UseDirectoryUrls = directoryUrls };
        var plugin = new GeneratedPagesPlugin(stream, [new(path, [.. GeneratedBytes])]);

        var first = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [plugin], options, cancellationToken);
        var second = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [plugin], options, cancellationToken);

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(1);
        await Assert.That(logger.Messages.Count).IsEqualTo(RepeatedBuildCount);
        await Assert.That(logger.Messages[0]).Contains("Source page");
        await Assert.That(logger.Messages[0]).Contains("is ignored for this build because generated page");
        await Assert.That(logger.Messages[0]).Contains(path);
        await Assert.That(await File.ReadAllTextAsync(sourcePath, cancellationToken)).IsEqualTo(source);
        var output = BuildPipelinePageProcessor.OutputPathFor(fixture.Output, path, directoryUrls);
        var html = await File.ReadAllTextAsync(output, cancellationToken);
        await Assert.That(html).Contains(GeneratedHeading);
        await Assert.That(html).DoesNotContain("Authored page");
        await Assert.That(Directory.GetFiles(fixture.Output, "*.html", SearchOption.AllDirectories).Length).IsEqualTo(1);
    }

    /// <summary>Missing source directories are not created for generated Markdown.</summary>
    /// <param name="stream">Whether generation uses an asynchronous stream.</param>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GeneratedPagesExistOnlyInOutput(bool stream, CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        var plugin = new GeneratedPagesPlugin(stream, [new("generated/index.md", [.. GeneratedBytes])]);
        var options = BuildPipelineOptions.Default with { UseDirectoryUrls = true };

        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [plugin], options, cancellationToken);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "generated/index.html"))).IsTrue();
        await Assert.That(Directory.GetFileSystemEntries(fixture.Input)).IsEmpty();
    }

    /// <summary>Excluded generated drafts leave the authored page available.</summary>
    /// <param name="includeDrafts">Whether generated drafts should be published.</param>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GeneratedDraftReservesOutputOnlyWhenIncluded(bool includeDrafts, CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, IndexFileName), SourceMarkdown, cancellationToken);
        var logger = new WarningLogger();
        var plugin = new GeneratedPagesPlugin(true, [new(IndexFileName, [.. DraftMarkdown])]);
        var options = BuildPipelineOptions.Default with { IncludeDrafts = includeDrafts, Logger = logger };

        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [plugin], options, cancellationToken);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(logger.Messages.Count).IsEqualTo(includeDrafts ? 1 : 0);
        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "index.html"), cancellationToken);
        await Assert.That(html).Contains(includeDrafts ? "Generated draft" : "Authored page");
    }

    /// <summary>Different source names that map to one directory URL cannot write that output twice.</summary>
    /// <param name="directoryUrls">Whether the paths share an output.</param>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OutputAliasesHaveOneOwner(bool directoryUrls, CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "guide.md"), SourceMarkdown, cancellationToken);
        var logger = new WarningLogger();
        var plugin = new GeneratedPagesPlugin(false, [new("guide/index.md", [.. GeneratedBytes])]);
        var options = BuildPipelineOptions.Default with { UseDirectoryUrls = directoryUrls, Logger = logger };

        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [plugin], options, cancellationToken);

        await Assert.That(count).IsEqualTo(directoryUrls ? 1 : DistinctPageCount);
        await Assert.That(logger.Messages.Count).IsEqualTo(directoryUrls ? 1 : 0);
        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "guide/index.html"), cancellationToken);
        await Assert.That(html).Contains(GeneratedHeading);
    }

    /// <summary>Repeated generated registrations cannot create duplicate outputs or cache entries.</summary>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task DuplicateGeneratedPagesWarnAndRenderOnce(CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        var logger = new WarningLogger();
        var plugin = new GeneratedPagesPlugin(
            true,
            [
                new(IndexFileName, [.. GeneratedBytes]),
                new(IndexFileName, "# Duplicate generation"u8.ToArray()),
            ]);
        var options = BuildPipelineOptions.Default with { Logger = logger };

        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [plugin], options, cancellationToken);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(logger.Messages.Count).IsEqualTo(1);
        await Assert.That(logger.Messages[0]).Contains(GeneratedHeading);
        await Assert.That(logger.Messages[0]).Contains("is ignored");
        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "index.html"), cancellationToken);
        await Assert.That(html).Contains(GeneratedHeading);
        await Assert.That(html).DoesNotContain("Duplicate generation");
    }

    /// <summary>Output casing is detected during registry creation and reused without further filesystem access.</summary>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task RegistryCachesOutputFilesystemComparison()
    {
        using var fixture = TempBuildFixture.Create();
        _ = Directory.CreateDirectory(fixture.Output);
        var registry = new PageOutputRegistry(fixture.Output);
        var shell = new BuildPhaseShell(fixture.Input, fixture.Output, BuildPipelineOptions.Default, new PluginTimingTable(), NullLogger.Instance);
        Directory.Delete(fixture.Output);

        var accepted = registry.TryRegister(new(default, "cached.md", PageFlags.None), shell);

        await Assert.That(accepted).IsTrue();
        await Assert.That(Directory.Exists(fixture.Output)).IsFalse();
    }

    /// <summary>Case-only aliases follow the output volume's actual casing behavior.</summary>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task OutputCaseAliasesFollowFilesystem(CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        _ = Directory.CreateDirectory(fixture.Output);
        var ignoresCase = FileSystemPathComparison.GetComparer(fixture.Output).Equals("Guide.html", "guide.html");
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "Guide.md"), SourceMarkdown, cancellationToken);
        var logger = new WarningLogger();
        var plugin = new GeneratedPagesPlugin(true, [new("guide.md", [.. GeneratedBytes])]);
        var options = BuildPipelineOptions.Default with { Logger = logger };

        var count = await BuildPipeline.RunAsync(fixture.Input, fixture.Output, [plugin], options, cancellationToken);

        await Assert.That(count).IsEqualTo(ignoresCase ? 1 : DistinctPageCount);
        await Assert.That(logger.Messages.Count).IsEqualTo(ignoresCase ? 1 : 0);
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Output, "guide.html"), cancellationToken)).Contains(GeneratedHeading);
    }

    /// <summary>Supplies generated pages through either supported discovery mechanism.</summary>
    /// <param name="stream">Whether pages are streamed.</param>
    /// <param name="pages">Generated page content.</param>
    private sealed class GeneratedPagesPlugin(bool stream, SyntheticPage[] pages) : IBuildDiscoverPlugin
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "generated-page-fixture"u8;

        /// <inheritdoc/>
        public PluginPriority DiscoverPriority => PluginPriority.Normal;

        /// <inheritdoc/>
        public ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
        {
            if (stream)
            {
                context.SyntheticPages.RegisterStream(StreamAsync(cancellationToken));
                return ValueTask.CompletedTask;
            }

            foreach (var page in pages)
            {
                context.SyntheticPages.Add(page);
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>Yields the fixture pages in their declared order.</summary>
        /// <param name="cancellationToken">Test cancellation.</param>
        /// <returns>The generated pages.</returns>
        private async IAsyncEnumerable<SyntheticPage> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return page;
                await Task.CompletedTask;
            }
        }
    }

    /// <summary>Records serialized discovery warnings.</summary>
    private sealed class WarningLogger : ILogger
    {
        /// <summary>Gets the warning messages.</summary>
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
}
