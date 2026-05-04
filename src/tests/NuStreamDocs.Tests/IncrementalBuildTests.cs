// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Caching;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests for the content-hash incremental manifest.</summary>
public class IncrementalBuildTests
{
    /// <summary>An unchanged page should be skipped on the second build (no plugin hook firing).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsUnchangedPageOnRebuild()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "stable.md"), "# Hi");

        var counter = new CountingPlugin();
        var builder = new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .UsePlugin(counter);

        await builder.BuildAsync();
        await Assert.That(counter.PageHits).IsEqualTo(1);

        await builder.BuildAsync();
        await Assert.That(counter.PageHits).IsEqualTo(1);
    }

    /// <summary>An edited page should be re-rendered, while the unchanged sibling is skipped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReRendersOnlyChangedPages()
    {
        using var fixture = TempBuildFixture.Create();
        var stablePath = Path.Combine(fixture.Input, "stable.md");
        var changingPath = Path.Combine(fixture.Input, "changing.md");
        await File.WriteAllTextAsync(stablePath, "# Stable");
        await File.WriteAllTextAsync(changingPath, "# Original");

        var counter = new CountingPlugin();
        var builder = new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .UsePlugin(counter);

        await builder.BuildAsync();
        await Assert.That(counter.PageHits).IsEqualTo(2);

        await File.WriteAllTextAsync(changingPath, "# Updated");

        await builder.BuildAsync();
        await Assert.That(counter.PageHits).IsEqualTo(3);
    }

    /// <summary>The manifest file should be written under the output root after a successful build.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WritesManifestFile()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .BuildAsync();

        var manifestPath = Path.Combine(fixture.Output, BuildManifest.FileName);
        await Assert.That(File.Exists(manifestPath)).IsTrue();
    }

    /// <summary>An unchanged page is re-rendered when the plugin pipeline changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RebuildsWhenPluginFingerprintChanges()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "stable.md"), "# Hi");

        var builder = new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output);
        await builder.BuildAsync();

        var withPlugin = new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .UsePlugin(new AppendCommentPlugin());
        await withPlugin.BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "stable.html"));
        await Assert.That(html).Contains("pipeline-changed");
    }

    /// <summary>Simple render plugin used to prove the fingerprint invalidates stale output.</summary>
    private sealed class AppendCommentPlugin : IDocPlugin
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "append-comment"u8;

        /// <inheritdoc/>
        public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
        {
            _ = context;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            "<!--pipeline-changed-->"u8.CopyTo(context.Html.GetSpan("<!--pipeline-changed-->".Length));
            context.Html.Advance("<!--pipeline-changed-->".Length);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
        {
            _ = context;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }
    }
}
