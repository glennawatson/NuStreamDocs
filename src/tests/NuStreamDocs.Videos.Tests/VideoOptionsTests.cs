// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Videos.Tests;

/// <summary>Unit tests for <see cref="VideoOptions"/>, <see cref="VideoOptionsExtensions"/>, and <see cref="DocBuilderVideoExtensions"/>.</summary>
public class VideoOptionsTests
{
    /// <summary>Every fluent builder on <see cref="VideoOptionsExtensions"/> updates its target property without mutating other properties.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FluentExtensionsProduceExpectedOptions()
    {
        var containerBytes = "my-video"u8.ToArray();
        var parentBytes = "video.example.org"u8.ToArray();

        var updated = VideoOptions.Default
            .WithProviders(VideoProviders.YouTube | VideoProviders.Vimeo)
            .WithEmbedBareUrls(false)
            .WithEmbedImageLinks(false)
            .WithEmbedStandaloneLinks(true)
            .WithYouTubePrivacyEnhanced(false)
            .WithVimeoDoNotTrack(false)
            .WithLazyLoad(false)
            .WithIncludeStylesheet(false)
            .WithContainerClass(containerBytes)
            .WithTwitchParent(parentBytes);

        await Assert.That(updated.Providers).IsEqualTo(VideoProviders.YouTube | VideoProviders.Vimeo);
        await Assert.That(updated.EmbedBareUrls).IsFalse();
        await Assert.That(updated.EmbedImageLinks).IsFalse();
        await Assert.That(updated.EmbedStandaloneLinks).IsTrue();
        await Assert.That(updated.YouTubePrivacyEnhanced).IsFalse();
        await Assert.That(updated.VimeoDoNotTrack).IsFalse();
        await Assert.That(updated.LazyLoad).IsFalse();
        await Assert.That(updated.IncludeStylesheet).IsFalse();
        await Assert.That(updated.ContainerClass).IsSameReferenceAs(containerBytes);
        await Assert.That(updated.TwitchParent).IsSameReferenceAs(parentBytes);

        var fromSpans = updated
            .WithContainerClass("span-video"u8)
            .WithTwitchParent("span.example.org"u8);

        await Assert.That(fromSpans.ContainerClass.AsSpan().SequenceEqual("span-video"u8)).IsTrue();
        await Assert.That(fromSpans.TwitchParent.AsSpan().SequenceEqual("span.example.org"u8)).IsTrue();
    }

    /// <summary>Null byte-array and delegate arguments throw <see cref="ArgumentNullException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullArgumentsThrowArgumentNullException()
    {
        byte[]? nullBytes = null;
        Func<VideoOptions, VideoOptions>? nullConfigure = null;
        DocBuilder builder = new();

        await Assert.That(() => VideoOptions.Default.WithContainerClass(nullBytes!)).Throws<ArgumentNullException>();
        await Assert.That(() => VideoOptions.Default.WithTwitchParent(nullBytes!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.UseVideos(nullConfigure!)).Throws<ArgumentNullException>();
    }

    /// <summary>All three <c>UseVideos</c> overloads on <see cref="DocBuilder"/> return the same builder instance for fluent chaining.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DocBuilderOverloadsChainCleanly()
    {
        DocBuilder b1 = new();
        DocBuilder b2 = new();
        DocBuilder b3 = new();

        await Assert.That(b1.UseVideos()).IsSameReferenceAs(b1);
        await Assert.That(b2.UseVideos(VideoOptions.Default)).IsSameReferenceAs(b2);
        await Assert.That(b3.UseVideos(static o => o.WithProviders(VideoProviders.YouTube))).IsSameReferenceAs(b3);
    }
}
