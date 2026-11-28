// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.Llms.Tests;

/// <summary>End-to-end build, writer, and plugin lifecycle tests for <see cref="LlmsPlugin"/>.</summary>
public class LlmsPluginTests
{
    /// <summary>Shared subfolder name used in integration fixtures.</summary>
    private const string GuideFolder = "guide";

    /// <summary>File name of the generated index file.</summary>
    private const string LlmsIndexFile = "llms.txt";

    /// <summary>Sample heading-only page name.</summary>
    private const string HeadingOnlyPage = "only-heading.md";

    /// <summary>Full site build emits <c>llms.txt</c>, <c>llms-full.txt</c>, <c>.md</c> siblings, and the <c>&lt;head&gt;</c> link.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FullBuildEmitsIndexFullTextSiblingsAndHeadLink()
    {
        DirectoryPath tempRoot = Path.Combine(Path.GetTempPath(), $"nd-llms-{Guid.NewGuid():N}");
        var docsDir = tempRoot / "docs";
        var siteDir = tempRoot / "site";

        try
        {
            await SeedDocsAsync(docsDir);
            await new DocBuilder()
                .WithInput(docsDir)
                .WithOutput(siteDir)
                .WithSiteName("Acme Docs"u8)
                .WithSiteUrl("https://docs.acme.test"u8)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseLlms(static o => o.WithSiteSummary("Reference for Acme."u8))
                .BuildAsync();

            var indexText = Encoding.UTF8.GetString(await siteDir.File(LlmsIndexFile).ReadAllBytesAsync());
            var fullText = Encoding.UTF8.GetString(await siteDir.File("llms-full.txt").ReadAllBytesAsync());
            var installSibling = Encoding.UTF8.GetString(await (siteDir / GuideFolder).File("install.md").ReadAllBytesAsync());
            var setupSibling = Encoding.UTF8.GetString(await (siteDir / GuideFolder).File("setup.md").ReadAllBytesAsync());
            var blankSibling = Encoding.UTF8.GetString(await siteDir.File("blank.md").ReadAllBytesAsync());
            var indexHtml = Encoding.UTF8.GetString(await siteDir.File("index.html").ReadAllBytesAsync());

            const string ExpectedGuideBlock =
                "## Guide\n\n"
                + "- [Installation](https://docs.acme.test/guide/install/): Run dotnet add package NuStreamDocs.Llms.\n"
                + "- [Setup](https://docs.acme.test/guide/setup/)\n";

            await Assert.That(indexText).Contains("# Acme Docs\n\n> Reference for Acme.\n");
            await Assert.That(indexText).Contains(ExpectedGuideBlock);
            await Assert.That(indexText).Contains("- [Welcome](https://docs.acme.test/): Getting started with NuStreamDocs.\n");
            await Assert.That(indexText).DoesNotContain("Hidden");
            await Assert.That(fullText).Contains("Source: https://docs.acme.test/guide/install/");
            await Assert.That(installSibling).IsEqualTo("# Installation\n\nRun dotnet add package NuStreamDocs.Llms.\n");
            await Assert.That(setupSibling).IsEqualTo("# Setup\n");
            await Assert.That(blankSibling).IsEqualTo("# Blank\n");
            await Assert.That(indexHtml).Contains("href=\"/llms.txt\"");
        }
        finally
        {
            if (tempRoot.Exists())
            {
                tempRoot.DeleteRecursive(true);
            }
        }
    }

    /// <summary>Disabled full-text/siblings/head-link options, empty site metadata, and repeated configure cycles behave cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MinimalOptionsRelativeUrlsAndReconfigureWorkCleanly()
    {
        DirectoryPath tempRoot = Path.Combine(Path.GetTempPath(), $"nd-llms-min-{Guid.NewGuid():N}");
        try
        {
            var options = LlmsOptions.Default
                .WithEmitFullText(false)
                .WithEmitMarkdownSiblings(false)
                .WithEmitHeadLink(false);
            LlmsPlugin plugin = new(options);

            var pluginName = Encoding.UTF8.GetString(plugin.Name);
            var prioritiesOk = plugin.ConfigurePriority == PluginPriority.Normal
                               && plugin.ScanPriority == PluginPriority.Normal
                               && plugin.FinalizePriority == PluginPriority.Normal;

            ArrayBufferWriter<byte> headSink = new();
            plugin.WriteHeadExtra(headSink);

            BuildConfigureContext configureCtx = new(tempRoot, tempRoot, [plugin], new()) { SiteUrl = [.. "  https://trailing.example.com/  "u8] };
            await plugin.ConfigureAsync(configureCtx, CancellationToken.None);
            plugin.Scan(new("stale.md", "# Stale"u8, "<p>Stale</p>"u8));

            BuildConfigureContext emptyConfigure = new(tempRoot, tempRoot, [plugin], new());
            await plugin.ConfigureAsync(emptyConfigure, CancellationToken.None);
            await plugin.FinalizeAsync(new(tempRoot, [plugin]), CancellationToken.None);
            var emptyFinalizeSkipped = !tempRoot.File(LlmsIndexFile).Exists();

            plugin.Scan(new(default, "# EmptyPath"u8, default));
            plugin.Scan(new(HeadingOnlyPage, "# Just Heading"u8, default));
            await plugin.FinalizeAsync(new(tempRoot, [plugin]), CancellationToken.None);

            var indexText = Encoding.UTF8.GetString(await tempRoot.File(LlmsIndexFile).ReadAllBytesAsync());
            var noFullText = !tempRoot.File("llms-full.txt").Exists();
            var noSibling = !tempRoot.File(HeadingOnlyPage).Exists();

            ArrayBufferWriter<byte> fullFallbackSink = new();
            LlmsPageEntry emptyBodyEntry = new([.. "Sec"u8], [.. "only-heading.html"u8], HeadingOnlyPage, [.. "Just Heading"u8], [], []);
            LlmsWriter.BuildFullText(default, null, [emptyBodyEntry], fullFallbackSink);
            var fullFallback = Encoding.UTF8.GetString(fullFallbackSink.WrittenSpan);

            var defaultBuilder = new DocBuilder().UseLlms().UseLlms(options);

            await Assert.That(pluginName).IsEqualTo("llms");
            await Assert.That(prioritiesOk).IsTrue();
            await Assert.That(headSink.WrittenCount).IsEqualTo(0);
            await Assert.That(emptyFinalizeSkipped && noFullText && noSibling).IsTrue();
            await Assert.That(indexText).Contains("# Documentation\n\n## Overview\n\n- [Just Heading](/only-heading.html)\n");
            await Assert.That(fullFallback).Contains("# Documentation\n\n---\n\n# Just Heading\nSource: /only-heading.html\n\n");
            await Assert.That(defaultBuilder).IsNotNull();
        }
        finally
        {
            if (tempRoot.Exists())
            {
                tempRoot.DeleteRecursive(true);
            }
        }
    }

    /// <summary>Seeds sample Markdown pages under <paramref name="docsDir"/>.</summary>
    /// <param name="docsDir">Input docs directory.</param>
    /// <returns>Completion task.</returns>
    private static async Task SeedDocsAsync(DirectoryPath docsDir)
    {
        _ = (docsDir / GuideFolder).Create();
        await docsDir.File("index.md").WriteAllBytesAsync(
            "---\ntitle: Welcome\ndescription: Getting started with NuStreamDocs.\n---\n\nWelcome to the documentation.\n"u8.ToArray());
        await (docsDir / GuideFolder).File("install.md").WriteAllBytesAsync(
            "# Installation\n\nRun dotnet add package NuStreamDocs.Llms.\n"u8.ToArray());
        await (docsDir / GuideFolder).File("setup.md").WriteAllBytesAsync("# Setup\n"u8.ToArray());
        await docsDir.File("blank.md").WriteAllBytesAsync("---\ntitle: Blank\n---\n"u8.ToArray());
        await docsDir.File("secret.md").WriteAllBytesAsync("---\nllms: false\n---\n# Hidden\n"u8.ToArray());
    }
}
