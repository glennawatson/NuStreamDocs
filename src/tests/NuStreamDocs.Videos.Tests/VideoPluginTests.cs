// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using NuStreamDocs.Audit;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.MagicLink;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Plugins;
using NuStreamDocs.Privacy;

namespace NuStreamDocs.Videos.Tests;

/// <summary>Lifecycle, asset, cross-plugin pipeline, and accessibility audit tests for <see cref="VideoPlugin"/>.</summary>
public class VideoPluginTests
{
    /// <summary>Gets a sample Vimeo URL reused across short-circuit tests.</summary>
    private static ReadOnlySpan<byte> SampleVimeoBytes => "https://vimeo.com/76979871"u8;

    /// <summary>Plugin metadata, priorities, static assets, and head link toggle behave as documented.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MetadataAssetsAndHeadExtraFollowStylesheetOption()
    {
        VideoPlugin enabled = new();
        PluginPriority expectedLate = new(PluginBand.Late);
        FilePath expectedAssetPath = new("assets/extensions/video.css");
        var isNamedVideos = enabled.Name.SequenceEqual("videos"u8);

        await Assert.That(isNamedVideos).IsTrue();
        await Assert.That(enabled.ConfigurePriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(enabled.PreRenderPriority).IsEqualTo(expectedLate);
        await Assert.That(enabled.StaticAssets.Length).IsEqualTo(1);
        await Assert.That(enabled.StaticAssets[0].Path).IsEqualTo(expectedAssetPath);

        ArrayBufferWriter<byte> headSink = new();
        enabled.WriteHeadExtra(headSink);
        await Assert.That(Encoding.UTF8.GetString(headSink.WrittenSpan))
            .IsEqualTo("<link rel=\"stylesheet\" href=\"/assets/extensions/video.css\">");

        VideoPlugin disabled = new(VideoOptions.Default.WithIncludeStylesheet(false));
        await Assert.That(disabled.StaticAssets.Length).IsEqualTo(0);

        headSink.ResetWrittenCount();
        disabled.WriteHeadExtra(headSink);
        await Assert.That(headSink.WrittenCount).IsEqualTo(0);
    }

    /// <summary><see cref="VideoPlugin.NeedsRewrite"/> rejects non-video pages and disabled configurations without rewriting.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NeedsRewriteShortCircuitsNonVideoPagesAndDisabledModes()
    {
        VideoPlugin plugin = new();
        var emptyCheck = plugin.NeedsRewrite(default);
        var proseCheck = plugin.NeedsRewrite("Plain prose with no links at all."u8);
        var githubCheck = plugin.NeedsRewrite("See [GitHub](https://github.com/reactiveui/ReactiveUI) and https://learn.microsoft.com/dotnet."u8);
        var vimeoCheck = plugin.NeedsRewrite(SampleVimeoBytes);
        var mp4Check = plugin.NeedsRewrite("https://cdn.example.com/clip.mp4"u8);

        VideoPlugin noProviders = new(VideoOptions.Default.WithProviders(VideoProviders.None));
        var disabledProvidersCheck = noProviders.NeedsRewrite(SampleVimeoBytes);

        var allFormsOff = VideoOptions.Default
            .WithEmbedBareUrls(false)
            .WithEmbedImageLinks(false)
            .WithEmbedStandaloneLinks(false);
        VideoPlugin noForms = new(allFormsOff);
        var disabledFormsCheck = noForms.NeedsRewrite(SampleVimeoBytes);

        await Assert.That(emptyCheck).IsFalse();
        await Assert.That(proseCheck).IsFalse();
        await Assert.That(githubCheck).IsFalse();
        await Assert.That(vimeoCheck).IsTrue();
        await Assert.That(mp4Check).IsTrue();
        await Assert.That(disabledProvidersCheck).IsFalse();
        await Assert.That(disabledFormsCheck).IsFalse();
    }

    /// <summary><see cref="VideoPlugin.ConfigureAsync"/> derives Twitch <c>parent</c> from <see cref="BuildConfigureContext.SiteUrl"/> unless explicitly set.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureAsyncResolvesTwitchParentFromSiteUrlOrExplicitOption()
    {
        CrossPageMarkerRegistry markers = new();
        BuildConfigureContext withSiteUrl = new(new("/in"), new("/out"), [], markers) { SiteUrl = [.. "https://docs.reactiveui.net/guide/"u8] };

        VideoPlugin autoPlugin = new();
        await autoPlugin.ConfigureAsync(withSiteUrl, CancellationToken.None);
        var autoHtml = RenderSingleLine(autoPlugin, "https://www.twitch.tv/videos/1987654321"u8);
        await Assert.That(autoHtml).Contains("parent=docs.reactiveui.net");

        VideoPlugin explicitPlugin = new(VideoOptions.Default.WithTwitchParent("override.example.com"u8));
        await explicitPlugin.ConfigureAsync(withSiteUrl, CancellationToken.None);
        var explicitHtml = RenderSingleLine(explicitPlugin, "https://www.twitch.tv/videos/1987654321"u8);
        await Assert.That(explicitHtml).Contains("parent=override.example.com");
    }

    /// <summary>Full <see cref="DocBuilder"/> build embeds videos inside admonitions, tabs, and MagicLink output, and passes <see cref="PageAuditor"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FullBuildIntegratesWithAdmonitionsTabsMagicLinkPrivacyAndAudit()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"nd-videos-test-{Guid.NewGuid():N}");
        var docsDir = Path.Combine(tempRoot, "docs");
        var siteDir = Path.Combine(tempRoot, "site");
        _ = Directory.CreateDirectory(docsDir);

        try
        {
            const string Source = """
                                  # Video Showcase

                                  https://www.youtube.com/watch?v=dQw4w9WgXcQ

                                  !!! note "OneDrive Walkthrough"
                                      https://onedrive.live.com/embed?resid=12345!678&authkey=!AbCdEf

                                  === "Vimeo"
                                      ![Architecture Deep Dive](https://vimeo.com/76979871)

                                  === "Cloudflare & Loom"
                                      https://watch.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957

                                      https://www.loom.com/share/8a9b0c1d2e3f4a5b
                                  """;

            await File.WriteAllTextAsync(Path.Combine(docsDir, "index.md"), Source);
            _ = await BuildShowcaseSiteAsync(docsDir, siteDir);
            StaticAssetComposer.WriteAll([new VideoPlugin()], new(siteDir));

            await Assert.That(File.Exists(Path.Combine(siteDir, "assets", "extensions", "video.css"))).IsTrue();
            var renderedBytes = await File.ReadAllBytesAsync(Path.Combine(siteDir, "index.html"));
            var renderedHtml = Encoding.UTF8.GetString(renderedBytes);

            await Assert.That(renderedHtml).Contains("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ");
            await Assert.That(renderedHtml).Contains("https://onedrive.live.com/embed?resid=12345!678&amp;authkey=!AbCdEf&amp;em=2");
            await Assert.That(renderedHtml).Contains("https://player.vimeo.com/video/76979871?dnt=1");
            await Assert.That(renderedHtml).Contains("title=\"Architecture Deep Dive\"");
            await Assert.That(renderedHtml).Contains("https://iframe.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957");
            await Assert.That(renderedHtml).Contains("https://www.loom.com/embed/8a9b0c1d2e3f4a5b");

            const string HeadHtml =
                "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Videos</title></head>";
            var fullHtml = Encoding.UTF8.GetBytes($"{HeadHtml}<body><main>{renderedHtml}</main></body></html>");
            var diagnostics = PageAuditor.Audit(new("/index.html"), fullHtml, AuditOptions.Default);
            await Assert.That(diagnostics.Length).IsEqualTo(0);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    /// <summary>Builds the showcase site registering <see cref="VideoPlugin"/> ahead of <c>MagicLink</c>, admonitions, tabs, and privacy.</summary>
    /// <param name="docsDir">Source docs folder.</param>
    /// <param name="siteDir">Output site folder.</param>
    /// <returns>Build exit code task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<int> BuildShowcaseSiteAsync(string docsDir, string siteDir) =>
        new DocBuilder()
            .WithInput(docsDir)
            .WithOutput(siteDir)
            .WithSiteUrl("https://docs.example.com"u8)
            .UseVideos()
            .UseMagicLink()
            .UseAdmonitions()
            .UseTabs()
            .UsePrivacy()
            .BuildAsync();

    /// <summary>Runs <see cref="VideoPlugin.PreRender"/> on <paramref name="markdown"/> synchronously and returns UTF-8 text.</summary>
    /// <param name="plugin">Configured plugin instance.</param>
    /// <param name="markdown">Input UTF-8 markdown.</param>
    /// <returns>Decoded rewritten output.</returns>
    private static string RenderSingleLine(VideoPlugin plugin, ReadOnlySpan<byte> markdown)
    {
        ArrayBufferWriter<byte> sink = new();
        PagePreRenderContext ctx = new("p.md", markdown, sink);
        plugin.PreRender(in ctx);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
