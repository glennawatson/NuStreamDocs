// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.Images.Tests;

/// <summary>Unit and integration tests for <see cref="ImagesPlugin"/>, <see cref="ImagesHtmlRewriter"/>, and <see cref="ImagesOptionsExtensions"/>.</summary>
public sealed class ImagesPluginTests
{
    /// <summary>Sample PNG width.</summary>
    private const int BannerWidth = 800;

    /// <summary>Sample PNG height.</summary>
    private const int BannerHeight = 450;

    /// <summary>Shared index Markdown filename.</summary>
    private const string IndexMarkdownName = "index.md";

    /// <summary>Shared sample image tag string.</summary>
    private const string SampleImgTag = "<img src=\"a.png\">";

    /// <summary>Valid 800x450 PNG header bytes.</summary>
    private static readonly byte[] BannerPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, .. "IHDR"u8, 0, 0, 0x03, 0x20, 0, 0, 0x01, 0xC2];

    /// <summary>Gets the UTF-8 bytes of <see cref="SampleImgTag"/>.</summary>
    private static ReadOnlySpan<byte> SampleImgBytes => "<img src=\"a.png\">"u8;

    /// <summary>Verifies an end-to-end <see cref="DocBuilder"/> build injects dimensions, eager/lazy loading, async decoding, figure captions, and stylesheet assets.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocBuilder_UseImages_InjectsDimensionsLoadingAndFigureCaptions(CancellationToken cancellationToken)
    {
        var tempRoot = new DirectoryPath(Path.Combine(Path.GetTempPath(), $"nd-images-{Guid.NewGuid():N}"));
        try
        {
            var inputDir = (tempRoot / "docs").Create();
            var subDir = (inputDir / "guide").Create();
            var outputDir = tempRoot / "site";

            await inputDir.File("hero.png").WriteAllBytesAsync(BannerPngBytes, cancellationToken);
            await subDir.File("diagram.svg").WriteAllBytesAsync(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 320 180\"></svg>"u8.ToArray(),
                cancellationToken);

            await subDir.File(IndexMarkdownName).WriteAllBytesAsync(
                """
                # Guide

                ![Hero](/hero.png "Hero caption")

                ![Diagram](diagram.svg?v=1#top)
                """u8.ToArray(),
                cancellationToken);

            await new DocBuilder()
                .WithInput(inputDir)
                .WithOutput(outputDir)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseImages(static opts => opts.WithEagerFirstImages(1))
                .BuildAsync(cancellationToken);

            var html = Encoding.UTF8.GetString(await (outputDir / "guide").File("index.html").ReadAllBytesAsync(cancellationToken));
            await Assert.That(html).Contains("/assets/stylesheets/images.css");
            await Assert.That(html).Contains("<figure class=\"nd-figure\"><img ");
            await Assert.That(html).Contains("width=\"800\" height=\"450\" loading=\"eager\" decoding=\"async\"");
            await Assert.That(html).Contains("<figcaption class=\"nd-figcaption\">Hero caption</figcaption></figure>");
            await Assert.That(html).Contains("width=\"320\" height=\"180\" loading=\"lazy\" decoding=\"async\"");
            await Assert.That((outputDir / "assets" / "stylesheets").File("images.css").Exists()).IsTrue();
        }
        finally
        {
            if (tempRoot.Exists())
            {
                tempRoot.DeleteRecursive(true);
            }
        }
    }

    /// <summary>Verifies <see cref="ImagesOptions"/>, <see cref="ImagesOptionsExtensions"/>, and <see cref="DocBuilderImagesExtensions"/> overloads and guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OptionsAndBuilderExtensions_CoverAllFlagsAndGuards()
    {
        var customized = ImagesOptions.Default
            .WithEagerFirstImages(1)
            .WithoutDimensions()
            .WithoutLazyLoading()
            .WithoutAsyncDecoding()
            .WithoutFigureWrapping()
            .Disable();

        await Assert.That(customized.Enabled).IsFalse();
        await Assert.That(customized.InjectDimensions).IsFalse();
        await Assert.That(customized.LazyLoading).IsFalse();
        await Assert.That(customized.AsyncDecoding).IsFalse();
        await Assert.That(customized.WrapTitledInFigure).IsFalse();
        await Assert.That(customized.EagerFirstCount).IsEqualTo(1);

        await Assert.That(static () => ImagesOptions.Default.WithEagerFirstImages(-1)).Throws<ArgumentOutOfRangeException>();

        var builder = new DocBuilder().UseImages().UseImages(ImagesOptions.Default);
        await Assert.That(builder).IsNotNull();

        Func<ImagesOptions, ImagesOptions>? nullConfigure = null;
        await Assert.That(() => new DocBuilder().UseImages(nullConfigure!)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies <see cref="ImagesPlugin"/> disabled behavior, cache reuse, missing/corrupt files, and path-traversal rejection.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImagesPlugin_DisabledPathTraversalAndFileProbing_BehaveSafely(CancellationToken cancellationToken)
    {
        var defaultPlugin = new ImagesPlugin();
        var matchesName = defaultPlugin.Name.SequenceEqual("images"u8);
        await Assert.That(matchesName).IsTrue();
        await Assert.That(defaultPlugin.ConfigurePriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(defaultPlugin.PostRenderPriority.Band).IsEqualTo(PluginBand.Normal);
        await Assert.That(defaultPlugin.StaticAssets.Length).IsEqualTo(1);
        await Assert.That(defaultPlugin.NeedsRewrite(SampleImgBytes)).IsTrue();
        await Assert.That(defaultPlugin.NeedsRewrite("<p>none</p>"u8)).IsFalse();

        var disabled = new ImagesPlugin(ImagesOptions.Default.Disable());
        await Assert.That(disabled.StaticAssets.Length).IsEqualTo(0);
        await Assert.That(disabled.NeedsRewrite(SampleImgBytes)).IsFalse();

        var headBuf = new ArrayBufferWriter<byte>();
        disabled.WriteHeadExtra(headBuf);
        await Assert.That(headBuf.WrittenCount).IsEqualTo(0);

        var enabledHeadBuf = new ArrayBufferWriter<byte>();
        defaultPlugin.WriteHeadExtra(enabledHeadBuf);
        await Assert.That(enabledHeadBuf.WrittenCount).IsGreaterThan(0);

        var passThroughBuf = new ArrayBufferWriter<byte>();
        defaultPlugin.PostRender(new(IndexMarkdownName, default, "<p>plain</p>"u8, passThroughBuf));
        await Assert.That(Encoding.UTF8.GetString(passThroughBuf.WrittenSpan)).IsEqualTo("<p>plain</p>");

        var disabledRenderBuf = new ArrayBufferWriter<byte>();
        disabled.PostRender(new(IndexMarkdownName, default, SampleImgBytes, disabledRenderBuf));
        await Assert.That(Encoding.UTF8.GetString(disabledRenderBuf.WrittenSpan)).IsEqualTo(SampleImgTag);

        await Assert.That(ImagesPlugin.ResolveCandidateFilePath(default, default, "a.png"u8)).IsNull();
        await Assert.That(ImagesPlugin.ResolveCandidateFilePath("/tmp/docs", default, "?only-query"u8)).IsNull();
        await Assert.That(ImagesPlugin.ResolveCandidateFilePath("/tmp/docs", "sub", "../../outside.png"u8)).IsNull();
        await Assert.That(ImagesPlugin.ProbeFileDimensions("/nonexistent/file-nd-test.png")).IsNull();

        var tempDir = new DirectoryPath(Path.Combine(Path.GetTempPath(), $"nd-img-probe-{Guid.NewGuid():N}")).Create();
        try
        {
            var corruptPath = tempDir.File("bad.bin");
            await corruptPath.WriteAllBytesAsync("not-an-image"u8.ToArray(), cancellationToken);
            await Assert.That(ImagesPlugin.ProbeFileDimensions(corruptPath.Value)).IsNull();

            await defaultPlugin.ConfigureAsync(new(tempDir, default, [], new()), cancellationToken);
            var missingRenderBuf = new ArrayBufferWriter<byte>();
            defaultPlugin.PostRender(new(IndexMarkdownName, default, "<img src=\"missing.png\"><img src=\"bad.bin\"><img src=\"../../out.png\">"u8, missingRenderBuf));
            await Assert.That(Encoding.UTF8.GetString(missingRenderBuf.WrittenSpan)).DoesNotContain("width=");
        }
        finally
        {
            if (tempDir.Exists())
            {
                tempDir.DeleteRecursive(true);
            }
        }
    }

    /// <summary>Verifies <see cref="ImagesHtmlRewriter"/> handles figure nesting, self-closing tags, quoted brackets, remote URIs, and pre-existing attributes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImagesHtmlRewriter_CoversFigureNestingQuotesSelfClosingAndRemoteSources()
    {
        var html = """
            <figure><img src="a.png" title='Already in figure' alt="a > b" /></figure>
            </figure>
            <figurecaption>not-a-figure</figurecaption>
            <figure<img src="bare.png">
            <figure class="custom"><figcaption>x</figcaption><img src='b.png' title="Nested" width="10"></figure>
            <img src="c.png" height="20" loading="eager" decoding="sync" title="">
            <img src="https://cdn.test/r.png">
            <img src="http://cdn.test/r.png">
            <img src="//cdn.test/r.png">
            <img src="data:image/gif;base64,R0lGODlhAQABAAAAACw=">
            <img alt="no-src" data-width="5" bad=unquoted bareflag>
            <img alt="no-src" end=bare>
            """u8;

        var buf = new ArrayBufferWriter<byte>();
        ImagesHtmlRewriter.Rewrite(
            html,
            ImagesOptions.Default,
            static _ => new(BannerWidth, BannerHeight),
            buf);

        var result = Encoding.UTF8.GetString(buf.WrittenSpan);
        await Assert.That(result).Contains("width=\"800\" height=\"450\" loading=\"lazy\" decoding=\"async\" />");
        await Assert.That(result).DoesNotContain("<figure class=\"nd-figure\">");

        var unclosedTagBuf = new ArrayBufferWriter<byte>();
        ImagesHtmlRewriter.Rewrite(
            "<img src=\"a.png\" alt=\"unclosed"u8,
            ImagesOptions.Default.WithoutLazyLoading().WithoutAsyncDecoding().WithoutDimensions(),
            static _ => null,
            unclosedTagBuf);
        await Assert.That(Encoding.UTF8.GetString(unclosedTagBuf.WrittenSpan)).IsEqualTo("<img src=\"a.png\" alt=\"unclosed");

        var mutedBuf = new ArrayBufferWriter<byte>();
        ImagesHtmlRewriter.Rewrite(
            "<img src=\"\" width title=>"u8,
            ImagesOptions.Default.WithoutLazyLoading().WithoutAsyncDecoding(),
            static _ => null,
            mutedBuf);
        await Assert.That(Encoding.UTF8.GetString(mutedBuf.WrittenSpan)).IsEqualTo("<img src=\"\" width title=>");
    }
}
