// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.Social.Tests;

/// <summary>Unit and integration tests for <see cref="SocialPlugin"/>, <see cref="SocialHeadWriter"/>, and <see cref="DocBuilderSocialExtensions"/>.</summary>
public sealed class SocialPluginTests
{
    /// <summary>Shared social folder segment name.</summary>
    private const string SocialDirSegment = "social";

    /// <summary>Minimal HTML document containing a head close marker.</summary>
    private const string EmptyHeadHtml = "<head></head>";

    /// <summary>Normalized test site URL.</summary>
    private const string SampleBaseUrl = "https://a.test/";

    /// <summary>Gets the UTF-8 bytes of <see cref="EmptyHeadHtml"/>.</summary>
    private static ReadOnlySpan<byte> EmptyHeadBytes => "<head></head>"u8;

    /// <summary>Gets the UTF-8 bytes of <see cref="SampleBaseUrl"/>.</summary>
    private static ReadOnlySpan<byte> SampleBaseBytes => "https://a.test/"u8;

    /// <summary>Verifies an end-to-end <see cref="DocBuilder"/> build emits OpenGraph, Twitter, canonical, JSON-LD, and SVG preview cards.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocBuilder_UseSocial_EmitsMetaTagsJsonLdAndSvgCard(CancellationToken cancellationToken)
    {
        var tempRoot = new DirectoryPath(Path.Combine(Path.GetTempPath(), $"nd-social-{Guid.NewGuid():N}"));
        try
        {
            var inputDir = (tempRoot / "docs").Create();
            var outputDir = tempRoot / "site";

            await inputDir.File("index.md").WriteAllBytesAsync(
                "# Welcome\n\nFast UTF-8 documentation generator."u8.ToArray(),
                cancellationToken);
            await inputDir.File("custom.md").WriteAllBytesAsync(
                "---\ntitle: Custom Banner\nsocial_image: /img/banner.png\n---\n\nCustom image page."u8.ToArray(),
                cancellationToken);

            await new DocBuilder()
                .WithInput(inputDir)
                .WithOutput(outputDir)
                .WithSiteName("My Project"u8)
                .WithSiteAuthor("Jane Doe"u8)
                .WithSiteUrl("https://docs.example.com"u8)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseSocial(static opts => opts.WithTwitterSite("@myproject"u8))
                .BuildAsync(cancellationToken);

            var indexHtml = Encoding.UTF8.GetString(await outputDir.File("index.html").ReadAllBytesAsync(cancellationToken));
            await Assert.That(indexHtml).Contains("<link rel=\"canonical\" href=\"https://docs.example.com/\">");
            await Assert.That(indexHtml).Contains("<meta property=\"og:title\" content=\"Welcome\">");
            await Assert.That(indexHtml).Contains("<meta property=\"og:image\" content=\"https://docs.example.com/assets/social/index.svg\">");
            await Assert.That(indexHtml).Contains("<meta property=\"og:image:width\" content=\"1200\">");
            await Assert.That(indexHtml).Contains("<meta name=\"twitter:site\" content=\"@myproject\">");
            await Assert.That(indexHtml).Contains("\"@type\":\"TechArticle\"");
            await Assert.That(indexHtml).Contains("\"name\":\"Jane Doe\"");

            var socialOutput = outputDir / "assets" / SocialDirSegment;
            var indexSvgFile = socialOutput.File("index.svg");
            await Assert.That(indexSvgFile.Exists()).IsTrue();
            var svgContent = Encoding.UTF8.GetString(await indexSvgFile.ReadAllBytesAsync(cancellationToken));
            await Assert.That(svgContent).Contains("Welcome");

            var customHtml = Encoding.UTF8.GetString(await (outputDir / "custom").File("index.html").ReadAllBytesAsync(cancellationToken));
            await Assert.That(customHtml).Contains("<meta property=\"og:image\" content=\"https://docs.example.com/img/banner.png\">");
            await Assert.That(customHtml).DoesNotContain("og:image:type");
            await Assert.That(socialOutput.File("custom.svg").Exists()).IsFalse();
        }
        finally
        {
            if (tempRoot.Exists())
            {
                tempRoot.DeleteRecursive(true);
            }
        }
    }

    /// <summary>Verifies all three <see cref="DocBuilderSocialExtensions"/> overloads and null guard.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocBuilderExtensions_OverloadsAndNullGuard_WorkAsExpected()
    {
        var builder = new DocBuilder().UseSocial().UseSocial(SocialOptions.Default);
        await Assert.That(builder).IsNotNull();

        Func<SocialOptions, SocialOptions>? nullConfigure = null;
        await Assert.That(() => new DocBuilder().UseSocial(nullConfigure!)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies <see cref="SocialPlugin"/> priorities, pass-through cases, duplicate slug handling, and finalize guards.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SocialPlugin_DisabledMissingHeadDuplicateSlugAndEmptyFinalize_BehaveCleanly(CancellationToken cancellationToken)
    {
        var defaultPlugin = new SocialPlugin();
        var matchesName = defaultPlugin.Name.SequenceEqual("social"u8);
        await Assert.That(matchesName).IsTrue();
        await Assert.That(defaultPlugin.ConfigurePriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(defaultPlugin.FinalizePriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(defaultPlugin.PostRenderPriority.Band).IsEqualTo(PluginBand.Latest);
        await Assert.That(defaultPlugin.NeedsRewrite(EmptyHeadBytes)).IsTrue();
        await Assert.That(defaultPlugin.NeedsRewrite("<body>no head</body>"u8)).IsFalse();

        await defaultPlugin.FinalizeAsync(new(default, []), cancellationToken);

        var disabledPlugin = new SocialPlugin(SocialOptions.Default.Disable());
        await Assert.That(disabledPlugin.NeedsRewrite(EmptyHeadBytes)).IsFalse();

        var outDisabled = new ArrayBufferWriter<byte>();
        disabledPlugin.PostRender(new("a.md", "# A"u8, EmptyHeadBytes, outDisabled));
        await Assert.That(Encoding.UTF8.GetString(outDisabled.WrittenSpan)).IsEqualTo(EmptyHeadHtml);

        var outNoHead = new ArrayBufferWriter<byte>();
        defaultPlugin.PostRender(new("a.md", "# A"u8, "<p>Body</p>"u8, outNoHead));
        await Assert.That(Encoding.UTF8.GetString(outNoHead.WrittenSpan)).IsEqualTo("<p>Body</p>");

        await defaultPlugin.ConfigureAsync(
            new(default, default, [], new()),
            cancellationToken);

        var outFirst = new ArrayBufferWriter<byte>();
        defaultPlugin.PostRender(new("dup.md", "# Dup"u8, EmptyHeadBytes, outFirst));
        var outSecond = new ArrayBufferWriter<byte>();
        defaultPlugin.PostRender(new("dup.md", "# Dup"u8, EmptyHeadBytes, outSecond));
        await Assert.That(Encoding.UTF8.GetString(outSecond.WrittenSpan)).Contains("/assets/social/dup.svg");

        await defaultPlugin.FinalizeAsync(new(default, []), cancellationToken);
    }

    /// <summary>Verifies URL normalization, canonical composition, and image resolution branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UrlHelpers_CoverAllCanonicalAndImageBranches()
    {
        await Assert.That(SocialPlugin.NormalizeTrailingSlashUrl("   "u8).Length).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(SocialPlugin.NormalizeTrailingSlashUrl(SampleBaseBytes))).IsEqualTo(SampleBaseUrl);
        await Assert.That(Encoding.UTF8.GetString(SocialPlugin.NormalizeTrailingSlashUrl("https://a.test"u8))).IsEqualTo(SampleBaseUrl);

        await Assert.That(SocialPlugin.ComposeCanonicalUrl(default, "guide.md", false).Length).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(SocialPlugin.ComposeCanonicalUrl(SampleBaseBytes, "guide.md", false)))
            .IsEqualTo("https://a.test/guide.html");

        var httpsMeta = new SocialPageMetadata("T"u8.ToArray(), [], "https://cdn.test/img.png"u8.ToArray(), "s");
        await Assert.That(Encoding.UTF8.GetString(SocialPlugin.ResolveImageUrl(httpsMeta, SampleBaseBytes, true, out var genHttps)))
            .IsEqualTo("https://cdn.test/img.png");
        await Assert.That(genHttps).IsFalse();

        var httpMeta = new SocialPageMetadata("T"u8.ToArray(), [], "http://cdn.test/img.png"u8.ToArray(), "s");
        await Assert.That(Encoding.UTF8.GetString(SocialPlugin.ResolveImageUrl(httpMeta, SampleBaseBytes, true, out var genHttp)))
            .IsEqualTo("http://cdn.test/img.png");
        await Assert.That(genHttp).IsFalse();

        var relNoBase = new SocialPageMetadata("T"u8.ToArray(), [], "/local.png"u8.ToArray(), "s");
        await Assert.That(Encoding.UTF8.GetString(SocialPlugin.ResolveImageUrl(relNoBase, default, true, out _)))
            .IsEqualTo("/local.png");

        var noSvg = new SocialPageMetadata("T"u8.ToArray(), [], [], "s");
        await Assert.That(SocialPlugin.ResolveImageUrl(noSvg, SampleBaseBytes, false, out var genOff).Length).IsEqualTo(0);
        await Assert.That(genOff).IsFalse();
    }

    /// <summary>Verifies <see cref="SocialHeadWriter"/> respects individual section toggles, existing canonical links, and summary card fallbacks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SocialHeadWriter_SelectiveTogglesAndMissingOptionalFields_EmitExpectedMarkup()
    {
        var minimalPayload = new SocialHeadPayload("TitleOnly"u8.ToArray(), [], [], [], false, [], []);
        var bufMinimal = new ArrayBufferWriter<byte>();
        SocialHeadWriter.WriteHeadTags(bufMinimal, "<head>"u8, minimalPayload, SocialOptions.Default);

        var minimalHtml = Encoding.UTF8.GetString(bufMinimal.WrittenSpan);
        await Assert.That(minimalHtml).DoesNotContain("rel=\"canonical\"");
        await Assert.That(minimalHtml).Contains("<meta name=\"twitter:card\" content=\"summary\">");
        await Assert.That(minimalHtml).DoesNotContain("og:description");
        await Assert.That(minimalHtml).DoesNotContain("twitter:description");

        var existingCanonicalHead = "<head><link rel=\"canonical\" href=\"https://custom.test/\">"u8.ToArray();
        var fullPayload = new SocialHeadPayload(
            "Title"u8.ToArray(),
            [],
            "https://docs.test/p/"u8.ToArray(),
            [],
            false,
            [],
            []);
        var bufExistingCanonical = new ArrayBufferWriter<byte>();
        var allMuted = SocialOptions.Default
            .WithoutCanonicalLink()
            .WithoutOpenGraph()
            .WithoutTwitterCard()
            .WithoutJsonLd();
        SocialHeadWriter.WriteHeadTags(bufExistingCanonical, existingCanonicalHead, fullPayload, allMuted);
        await Assert.That(bufExistingCanonical.WrittenCount).IsEqualTo(0);

        var bufDupCanonical = new ArrayBufferWriter<byte>();
        SocialHeadWriter.WriteHeadTags(bufDupCanonical, existingCanonicalHead, fullPayload, SocialOptions.Default);
        await Assert.That(Encoding.UTF8.GetString(bufDupCanonical.WrittenSpan)).DoesNotContain("<link rel=\"canonical\"");
    }
}
