// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Videos.Tests;

/// <summary>Unit tests for <see cref="VideoUrlParser"/>, <see cref="VideoUrlProbe"/>, and <see cref="VideoEmbedEmitter"/> across all supported providers.</summary>
public class VideoUrlParserTests
{
    /// <summary>Expected YouTube BEM modifier.</summary>
    private const string YouTubeMod = "nd-video--youtube";

    /// <summary>Expected Vimeo BEM modifier.</summary>
    private const string VimeoMod = "nd-video--vimeo";

    /// <summary>Expected canonical Vimeo embed URL for video 76979871.</summary>
    private const string VimeoCanonicalSrc = "https://player.vimeo.com/video/76979871?dnt=1";

    /// <summary>Expected Microsoft Stream / SharePoint BEM modifier.</summary>
    private const string StreamMod = "nd-video--stream";

    /// <summary>Expected Azure BEM modifier.</summary>
    private const string AzureMod = "nd-video--azure";

    /// <summary>Expected HTML5 video BEM modifier.</summary>
    private const string Html5Mod = "nd-video--html5";

    /// <summary>Expected Dailymotion BEM modifier.</summary>
    private const string DailymotionMod = "nd-video--dailymotion";

    /// <summary>Expected canonical Dailymotion player URL.</summary>
    private const string DailymotionBaseSrc = "https://geo.dailymotion.com/player.html?video=x84sh87";

    /// <summary>Expected Twitch BEM modifier.</summary>
    private const string TwitchMod = "nd-video--twitch";

    /// <summary>Expected canonical Twitch Clip embed URL.</summary>
    private const string TwitchClipCanonicalSrc = "https://clips.twitch.tv/embed?clip=ExcitedClipSlug123&amp;parent=docs.example.com&amp;autoplay=false";

    /// <summary>Expected Cloudflare Stream BEM modifier.</summary>
    private const string CloudflareMod = "nd-video--cloudflare";

    /// <summary>Expected Bilibili BEM modifier.</summary>
    private const string BilibiliMod = "nd-video--bilibili";

    /// <summary>Expected Vidyard BEM modifier.</summary>
    private const string VidyardMod = "nd-video--vidyard";

    /// <summary>Expected canonical SharePoint <c>:v:</c> embedview URL.</summary>
    private const string SharePointEmbedViewUrl = "https://contoso.sharepoint.com/:v:/s/team/EaBcDeFg12345?action=embedview";

    /// <summary>Gets YouTube, Vimeo, and OneDrive test cases.</summary>
    /// <returns>Test cases for the first group of providers.</returns>
    public static IReadOnlyList<VideoUrlCase> YouTubeVimeoOneDriveCases() =>
    [
        new("https://www.youtube.com/watch?v=dQw4w9WgXcQ", YouTubeMod, "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ"),
        new("https://youtube.com/watch?v=dQw4w9WgXcQ&t=1h2m3s", YouTubeMod, "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=3723"),
        new("https://m.youtube.com/watch?v=dQw4w9WgXcQ&start=45", YouTubeMod, "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=45"),
        new("https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=PL12345&t=90", YouTubeMod, "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?list=PL12345&amp;start=90"),
        new("https://youtu.be/dQw4w9WgXcQ?t=30s", YouTubeMod, "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=30"),
        new("https://www.youtube.com/shorts/abc123XYZ", "nd-video--shorts", "https://www.youtube-nocookie.com/embed/abc123XYZ"),
        new("https://www.youtube.com/live/live98765", YouTubeMod, "https://www.youtube-nocookie.com/embed/live98765"),
        new("https://www.youtube.com/embed/emb12345", YouTubeMod, "https://www.youtube-nocookie.com/embed/emb12345"),
        new("https://www.youtube-nocookie.com/v/leg12345", YouTubeMod, "https://www.youtube-nocookie.com/embed/leg12345"),
        new("https://www.youtube.com/playlist?list=PL_abc-123", YouTubeMod, "https://www.youtube-nocookie.com/embed/videoseries?list=PL_abc-123"),
        new("https://vimeo.com/76979871", VimeoMod, VimeoCanonicalSrc),
        new("https://www.vimeo.com/76979871/8f7e6d5c4b#t=1m20s", VimeoMod, "https://player.vimeo.com/video/76979871?h=8f7e6d5c4b&amp;dnt=1#t=1m20s"),
        new("https://www.vimeo.com/76979871/8f7e6d5c4b?autoplay=1", VimeoMod, "https://player.vimeo.com/video/76979871?h=8f7e6d5c4b&amp;autoplay=1&amp;dnt=1"),
        new("https://www.vimeo.com/76979871/8f7e6d5c4b?h=8f7e6d5c4b", VimeoMod, "https://player.vimeo.com/video/76979871?h=8f7e6d5c4b&amp;dnt=1"),
        new("https://vimeo.com/channels/staffpicks/76979871", VimeoMod, VimeoCanonicalSrc),
        new("https://vimeo.com/groups/motion/videos/76979871", VimeoMod, VimeoCanonicalSrc),
        new("https://vimeo.com/showcase/12345/video/76979871", VimeoMod, VimeoCanonicalSrc),
        new("https://vimeo.com/album/999/video/76979871", VimeoMod, VimeoCanonicalSrc),
        new("https://player.vimeo.com/video/76979871?autoplay=0&dnt=0", VimeoMod, "https://player.vimeo.com/video/76979871?autoplay=0&amp;dnt=0"),
        new(
            "https://onedrive.live.com/embed?resid=12345!678&authkey=!AbCdEf",
            "nd-video--onedrive",
            "https://onedrive.live.com/embed?resid=12345!678&amp;authkey=!AbCdEf&amp;em=2"),
        new("https://onedrive.live.com/?id=999888&cid=111&em=2", "nd-video--onedrive", "https://onedrive.live.com/embed?id=999888&amp;cid=111&amp;em=2"),
    ];

    /// <summary>Gets SharePoint, Microsoft Stream, Azure, Loom, and Dailymotion test cases.</summary>
    /// <returns>Test cases for Microsoft, Azure, Loom, and Dailymotion providers.</returns>
    public static IReadOnlyList<VideoUrlCase> MicrosoftAzureLoomDailymotionCases() =>
    [
        new(
            "https://web.microsoftstream.com/video/f6e5d4c3-b2a1-0000-1111-222233334444?st=15",
            StreamMod,
            "https://web.microsoftstream.com/embed/video/f6e5d4c3-b2a1-0000-1111-222233334444?st=15"),
        new("https://web.microsoftstream.com/embed/video/f6e5d4c3-b2a1", StreamMod, "https://web.microsoftstream.com/embed/video/f6e5d4c3-b2a1"),
        new(
            "https://contoso.sharepoint.com/sites/eng/_layouts/15/stream.aspx?id=%2Fsites%2Feng%2Fdemo.mp4",
            StreamMod,
            "https://contoso.sharepoint.com/sites/eng/_layouts/15/embed.aspx?id=%2Fsites%2Feng%2Fdemo.mp4"),
        new(
            "https://contoso-my.sharepoint.com/personal/alice/_layouts/15/embed.aspx?UniqueId=1234-5678",
            StreamMod,
            "https://contoso-my.sharepoint.com/personal/alice/_layouts/15/embed.aspx?UniqueId=1234-5678&amp;embed=true"),
        new(
            "https://contoso.sharepoint.com/:v:/s/team/EaBcDeFg12345",
            StreamMod,
            SharePointEmbedViewUrl),
        new(
            "https://contoso.sharepoint.com/:v:/s/team/EaBcDeFg12345?e=xyz987",
            StreamMod,
            "https://contoso.sharepoint.com/:v:/s/team/EaBcDeFg12345?e=xyz987&amp;action=embedview"),
        new(
            SharePointEmbedViewUrl,
            StreamMod,
            SharePointEmbedViewUrl),
        new(
            "https://www.videoindexer.ai/accounts/00000000-0000-0000-0000-000000000000/videos/vid12345",
            AzureMod,
            "https://www.videoindexer.ai/embed/player/00000000-0000-0000-0000-000000000000/vid12345/"),
        new(
            "https://vi.azure.com/accounts/acc-01/videos/vid-02?location=trial",
            AzureMod,
            "https://www.videoindexer.ai/embed/player/acc-01/vid-02/?location=trial"),
        new("https://videoindexer.ai/embed/player/acc-01/vid-02/extra", AzureMod, "https://www.videoindexer.ai/embed/player/acc-01/vid-02/"),
        new(
            "https://amp.azure.net/libs/amp/latest/player.html?url=https%3A%2F%2Fmedia.example.net%2Fmanifest#t=5",
            AzureMod,
            "https://amp.azure.net/libs/amp/latest/player.html?url=https%3A%2F%2Fmedia.example.net%2Fmanifest"),
        new(
            "https://myaccount.blob.core.windows.net/videos/intro.mp4?sv=2025-01-01&sig=abc",
            Html5Mod,
            "src=\"https://myaccount.blob.core.windows.net/videos/intro.mp4?sv=2025-01-01&amp;sig=abc\" type=\"video/mp4\""),
        new(
            "https://docsmedia.azurefd.net/clips/walkthrough.webm",
            Html5Mod,
            "src=\"https://docsmedia.azurefd.net/clips/walkthrough.webm\" type=\"video/webm\""),
        new("https://www.loom.com/share/8a9b0c1d2e3f4a5b?sid=abc", "nd-video--loom", "https://www.loom.com/embed/8a9b0c1d2e3f4a5b?sid=abc"),
        new("https://loom.com/embed/8a9b0c1d2e3f4a5b", "nd-video--loom", "https://www.loom.com/embed/8a9b0c1d2e3f4a5b"),
        new("https://www.dailymotion.com/video/x84sh87_launch-keynote", DailymotionMod, DailymotionBaseSrc),
        new("https://www.dailymotion.com/embed/video/x84sh87", DailymotionMod, DailymotionBaseSrc),
        new("https://dai.ly/x84sh87?start=12", DailymotionMod, $"{DailymotionBaseSrc}&amp;start=12"),
        new("https://geo.dailymotion.com/player.html?video=x84sh87#frag", DailymotionMod, DailymotionBaseSrc),
    ];

    /// <summary>Gets Twitch, Wistia, Cloudflare Stream, Google Drive, Bilibili, Vidyard, and HTML5 test cases.</summary>
    /// <returns>Test cases for the remaining providers.</returns>
    public static IReadOnlyList<VideoUrlCase> TwitchWistiaCloudflareDriveBilibiliVidyardHtml5Cases() =>
    [
        new(
            "https://www.twitch.tv/videos/1987654321",
            TwitchMod,
            "https://player.twitch.tv/?video=1987654321&amp;parent=docs.example.com&amp;autoplay=false"),
        new("https://www.twitch.tv/reactiveui/clip/ExcitedClipSlug123", TwitchMod, TwitchClipCanonicalSrc),
        new("https://clips.twitch.tv/ExcitedClipSlug123", TwitchMod, TwitchClipCanonicalSrc),
        new("https://clips.twitch.tv/embed?clip=ExcitedClipSlug123", TwitchMod, TwitchClipCanonicalSrc),
        new("https://player.twitch.tv/?video=1987654321", TwitchMod, "https://player.twitch.tv/?video=1987654321&amp;parent=docs.example.com"),
        new("https://player.twitch.tv/?channel=reactiveui", TwitchMod, "https://player.twitch.tv/?channel=reactiveui&amp;parent=docs.example.com"),
        new("https://reactiveui.wistia.com/medias/wistia987abc", "nd-video--wistia", "https://fast.wistia.net/embed/iframe/wistia987abc"),
        new("https://fast.wistia.net/embed/iframe/wistia987abc?videoFoam=true", "nd-video--wistia", "https://fast.wistia.net/embed/iframe/wistia987abc?videoFoam=true"),
        new(
            "https://customer-f33zs165nr7gyfy4.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957/watch",
            CloudflareMod,
            "https://customer-f33zs165nr7gyfy4.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957/iframe"),
        new(
            "https://watch.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957",
            CloudflareMod,
            "https://iframe.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957"),
        new(
            "https://videodelivery.net/6b9e68b07dfee8cc2d116e4c51d6a957/iframe?muted=true",
            CloudflareMod,
            "https://iframe.videodelivery.net/6b9e68b07dfee8cc2d116e4c51d6a957?muted=true"),
        new(
            "https://drive.google.com/file/d/1aBcDeFgHiJkLmNoPqRsTuVwXyZ",
            "nd-video--gdrive",
            "https://drive.google.com/file/d/1aBcDeFgHiJkLmNoPqRsTuVwXyZ/preview"),
        new(
            "https://drive.google.com/file/d/1aBcDeFgHiJkLmNoPqRsTuVwXyZ/view?usp=sharing",
            "nd-video--gdrive",
            "https://drive.google.com/file/d/1aBcDeFgHiJkLmNoPqRsTuVwXyZ/preview"),
        new(
            "https://www.bilibili.com/video/BV1xx411c7mD?p=2",
            BilibiliMod,
            "https://player.bilibili.com/player.html?bvid=BV1xx411c7mD&amp;page=2&amp;autoplay=0"),
        new("https://bilibili.com/video/av170001", BilibiliMod, "https://player.bilibili.com/player.html?aid=170001&amp;autoplay=0"),
        new("https://player.bilibili.com/player.html?aid=170001", BilibiliMod, "https://player.bilibili.com/player.html?aid=170001"),
        new("https://share.vidyard.com/watch/AbCdEfGh12345678", VidyardMod, "https://play.vidyard.com/AbCdEfGh12345678.html"),
        new("https://play.vidyard.com/AbCdEfGh12345678", VidyardMod, "https://play.vidyard.com/AbCdEfGh12345678.html"),
        new("https://play.vidyard.com/AbCdEfGh12345678.html?autoplay=0", VidyardMod, "https://play.vidyard.com/AbCdEfGh12345678.html?autoplay=0"),
        new("https://cdn.example.com/releases/demo.ogv", Html5Mod, "src=\"https://cdn.example.com/releases/demo.ogv\" type=\"video/ogg\""),
        new(
            "http://cdn.example.com/releases/demo.mov",
            Html5Mod,
            "<video controls preload=\"metadata\" playsinline title=\"Embedded video\" src=\"http://cdn.example.com/releases/demo.mov\"></video>"),
    ];

    /// <summary>Gets primary non-video URLs, channel pages, profile links, and malformed inputs that must be rejected.</summary>
    /// <returns>Rejected URL strings.</returns>
    public static IReadOnlyList<string> PrimaryRejectedUrlCases() =>
    [
        string.Empty,
        "ftp://www.youtube.com/watch?v=dQw4w9WgXcQ",
        "https://",
        "https://github.com/reactiveui/ReactiveUI",
        "https://www.youtube.com/channel/UCWVSpLajyZcooAngLV3ItaQ",
        "https://www.youtube.com/@ReactiveUI",
        "https://www.youtube.com/watch?list=PL123",
        "https://www.youtube.com/playlist",
        "https://www.youtube.com/shorts/",
        "https://www.youtube.com/shorts/bad!id",
        "https://www.youtube.com/embed/bad!id",
        "https://www.youtube.com/watch?v=bad!id",
        "https://youtu.be/",
        "https://youtu.be/bad!id",
        "https://youtu.be/a/b",
        "https://vimeo.com/",
        "https://vimeo.com/about",
        "https://vimeo.com/76979871/bad!hash",
        "https://vimeo.com/12345/hash/extra",
        "https://vimeo.com/channels/staffpicks/notdigits",
        "https://vimeo.com/unknown/76979871",
        "https://player.vimeo.com/other",
        "https://player.vimeo.com/video/notdigits",
        "https://onedrive.live.com/about",
        "https://onedrive.live.com/?cid=onlycid",
        "https://web.microsoftstream.com/other",
        "https://web.microsoftstream.com/video/bad!id",
        "https://contoso.sharepoint.com/sites/eng/SitePages/Home.aspx",
        "https://contoso.sharepoint.com/sites/eng/_layouts/15/stream.aspx",
        "https://www.videoindexer.ai/other",
        "https://www.videoindexer.ai/accounts/only-account",
        "https://www.videoindexer.ai/accounts/bad!acct/videos/vid1",
        "https://www.videoindexer.ai/accounts/acc1/videos/bad!vid",
        "https://www.videoindexer.ai/embed/player/only-one",
        "https://www.videoindexer.ai/embed/player/bad!acct/vid1",
        "https://www.videoindexer.ai/embed/player/acc1/bad!vid",
        "https://www.videoindexer.ai/embed/player/acc1//vid1",
    ];

    /// <summary>Gets secondary non-video URLs and malformed inputs that must be rejected.</summary>
    /// <returns>Rejected URL strings.</returns>
    public static IReadOnlyList<string> SecondaryRejectedUrlCases() =>
    [
        "https://amp.azure.net/libs/amp/latest/player.html",
        "https://amp.azure.net/libs/amp/latest/other.html?url=x",
        "https://amp.azure.net/other/player.html?url=x",
        "https://myaccount.blob.core.windows.net/docs/readme.pdf",
        "https://www.loom.com/pricing",
        "https://www.loom.com/share/bad!id",
        "https://www.dailymotion.com/us",
        "https://dai.ly/bad!id",
        "https://geo.dailymotion.com/other?video=x84",
        "https://geo.dailymotion.com/player.html",
        "https://geo.dailymotion.com/player.html?video=bad!id",
        "https://www.twitch.tv/reactiveui",
        "https://www.twitch.tv/videos/notdigits",
        "https://www.twitch.tv/chan/clip/a/b",
        "https://www.twitch.tv/chan/clip/bad!slug",
        "https://clips.twitch.tv/",
        "https://clips.twitch.tv/bad!slug",
        "https://clips.twitch.tv/embed",
        "https://clips.twitch.tv/embed?clip=bad!slug",
        "https://player.twitch.tv/?muted=true",
        "https://wistia.com/product",
        "https://wistia.com/medias/bad!id",
        "https://watch.cloudflarestream.com/",
        "https://watch.cloudflarestream.com/bad!uid",
        "https://watch.cloudflarestream.com/uid/thumbnail.jpg",
        "https://drive.google.com/drive/folders/12345",
        "https://drive.google.com/file/d/",
        "https://drive.google.com/file/d/bad!id",
        "https://www.bilibili.com/other",
        "https://www.bilibili.com/video/BV",
        "https://www.bilibili.com/video/BVbad!id",
        "https://www.bilibili.com/video/xx12345",
        "https://www.bilibili.com/video/avNotDigits",
        "https://player.bilibili.com?bvid=BV123",
        "https://player.bilibili.com/other?bvid=BV123",
        "https://player.bilibili.com/player.html?foo=1",
        "https://share.vidyard.com/",
        "https://share.vidyard.com/watch/bad!id",
        "https://play.vidyard.com/",
        "https://play.vidyard.com/bad!id",
        "https://www.youtube.com/watch?v=bad\"quote",
    ];

    /// <summary>Every supported video provider URL parses and emits its canonical embed <c>src</c> and wrapper class.</summary>
    /// <param name="testCase">Input URL and expected output fragments.</param>
    /// <returns>Async test.</returns>
    [Test]
    [MethodDataSource(nameof(YouTubeVimeoOneDriveCases))]
    [MethodDataSource(nameof(MicrosoftAzureLoomDailymotionCases))]
    [MethodDataSource(nameof(TwitchWistiaCloudflareDriveBilibiliVidyardHtml5Cases))]
    public async Task SupportedProviderUrlParsesAndEmitsCanonicalHtml(VideoUrlCase testCase)
    {
        var (probed, parsed, html) = ParseAndEmit(testCase.InputUrl);
        await Assert.That(probed).IsTrue();
        await Assert.That(parsed).IsTrue();
        await Assert.That(html).Contains(testCase.ExpectedModifier);
        await Assert.That(html).Contains(testCase.ExpectedSrc);
    }

    /// <summary>Non-video URLs, channel pages, profile links, and malformed inputs are rejected without throwing.</summary>
    /// <param name="url">Non-embeddable URL candidate.</param>
    /// <returns>Async test.</returns>
    [Test]
    [MethodDataSource(nameof(PrimaryRejectedUrlCases))]
    [MethodDataSource(nameof(SecondaryRejectedUrlCases))]
    public async Task NonVideoOrMalformedUrlIsRejected(string url)
    {
        var bytes = Encoding.UTF8.GetBytes(url);
        var parsed = VideoUrlParser.TryParse(bytes, VideoProviders.All, out _);
        await Assert.That(parsed).IsFalse();
    }

    /// <summary>Selective <see cref="VideoProviders"/> flags allow only enabled providers and reject disabled ones.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SelectiveProviderFlagsFilterMatches()
    {
        var vimeoUrl = "https://vimeo.com/76979871"u8.ToArray();
        var youtubeParse = VideoUrlParser.TryParse(vimeoUrl, VideoProviders.YouTube, out _);
        var noneParse = VideoUrlParser.TryParse(vimeoUrl, VideoProviders.None, out _);
        var vimeoParse = VideoUrlParser.TryParse(vimeoUrl, VideoProviders.Vimeo, out _);
        var youtubeProbe = VideoUrlProbe.MayContainEmbeddableUrl(vimeoUrl, VideoProviders.YouTube);
        var noneProbe = VideoUrlProbe.MayContainEmbeddableUrl(vimeoUrl, VideoProviders.None);

        await Assert.That(youtubeParse).IsFalse();
        await Assert.That(noneParse).IsFalse();
        await Assert.That(vimeoParse).IsTrue();
        await Assert.That(youtubeProbe).IsFalse();
        await Assert.That(noneProbe).IsFalse();
    }

    /// <summary>Host probe rejects URLs exceeding DNS maximum host length and non-HTTP schemes containing <c>://</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ProbeHandlesOversizeHostsPortNumbersAndCustomSchemes()
    {
        const int OversizeHostLength = 260;
        var customProbe = VideoUrlProbe.MayContainEmbeddableUrl("custom://www.youtube.com/watch?v=dQw4w9WgXcQ"u8, VideoProviders.All);
        var longHostUrl = Encoding.UTF8.GetBytes($"https://{new string('a', OversizeHostLength)}.sharepoint.com/:v:/s/t/id");
        var longHostProbe = VideoUrlProbe.MayContainEmbeddableUrl(longHostUrl, VideoProviders.All);
        var longHostParse = VideoUrlParser.TryParse(longHostUrl, VideoProviders.All, out _);

        var portParse = VideoUrlParser.TryParse(
            "https://www.youtube.com:443/watch?v=dQw4w9WgXcQ&t=invalid"u8,
            VideoProviders.YouTube,
            out var match);
        var startSeconds = match.StartSeconds;

        await Assert.That(customProbe).IsFalse();
        await Assert.That(longHostProbe).IsFalse();
        await Assert.That(longHostParse).IsFalse();
        await Assert.That(portParse).IsTrue();
        await Assert.That(startSeconds).IsEqualTo(-1);
    }

    /// <summary>Query/duration/host parser edge cases and Twitch existing-parent/empty-parent fallbacks behave deterministically.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QueryDurationAndTwitchParentEdgeCasesAreHandled()
    {
        const int ExpectedOneMinFifteenSec = 75;
        var hostFromRelative = VideoUrlProbe.TryExtractHost("/relative/path"u8, out _);
        var hostFromEmptyScheme = VideoUrlProbe.TryExtractHost("https://"u8, out _);
        var probeNoTrailingSlash = VideoUrlProbe.MayContainEmbeddableUrl("https://vimeo.com"u8, VideoProviders.All);
        var probeEmptyHost = VideoUrlProbe.MayContainEmbeddableUrl("https:///watch"u8, VideoProviders.All);
        var probeShortScheme = VideoUrlProbe.MayContainEmbeddableUrl("a://b"u8, VideoProviders.All);
        var probeHtml5WithQuery = VideoUrlProbe.MayContainEmbeddableUrl("https://cdn.example.com/clip.mp4?v=1"u8, VideoProviders.Html5File);
        var emptySafeId = VideoUrlParser.IsSafeId(default);
        var emptyDigits = VideoUrlParser.IsAllAsciiDigits(default);

        var emptyFirstParam = VideoUrlParser.TryParse(
            "https://www.youtube.com/watch/?v=&v=dQw4w9WgXcQ&list=bad/id&t=1m15"u8,
            VideoProviders.YouTube,
            out var youtubeMatch);
        var youtubeSeconds = youtubeMatch.StartSeconds;
        var youtubeListEmpty = youtubeMatch.Query.IsEmpty;

        var zeroDuration = VideoUrlParser.TryParse("https://youtu.be/dQw4w9WgXcQ?t=0s"u8, VideoProviders.YouTube, out var zeroMatch);
        var zeroSec = zeroMatch.StartSeconds;
        _ = VideoUrlParser.TryParse("https://youtu.be/dQw4w9WgXcQ?t=1x"u8, VideoProviders.YouTube, out var badUnitMatch);
        var badUnitSec = badUnitMatch.StartSeconds;
        _ = VideoUrlParser.TryParse("https://www.youtube.com/shorts/abc123/extra"u8, VideoProviders.YouTube, out var shortsExtra);
        var shortsMatched = shortsExtra.Kind is VideoProviderKind.YouTubeShorts;

        var bilibiliBadPage = VideoUrlParser.TryParse(
            "https://www.bilibili.com/video/BV1xx411c7mD?p=abc"u8,
            VideoProviders.Bilibili,
            out var biliMatch);
        var biliPageEmpty = biliMatch.Query.IsEmpty;

        var indexerTrailingPath = VideoUrlParser.TryParse(
            "https://www.videoindexer.ai/accounts/acc1/videos/vid2/insights"u8,
            VideoProviders.Azure,
            out _);

        var twitchHtml = EmitWithEmptyParent("https://player.twitch.tv/?video=123&parent=already.example.com"u8);
        var twitchFallbackHtml = EmitWithEmptyParent("https://www.twitch.tv/videos/123"u8);
        var nonNoCookiePlaylistHtml = EmitNonPrivacyPlaylist("https://www.youtube.com/playlist?list=PL123"u8);

        await Assert.That(hostFromRelative).IsFalse();
        await Assert.That(hostFromEmptyScheme).IsFalse();
        await Assert.That(probeNoTrailingSlash).IsTrue();
        await Assert.That(probeEmptyHost).IsFalse();
        await Assert.That(probeShortScheme).IsFalse();
        await Assert.That(probeHtml5WithQuery).IsTrue();
        await Assert.That(emptySafeId).IsFalse();
        await Assert.That(emptyDigits).IsFalse();
        await Assert.That(emptyFirstParam).IsTrue();
        await Assert.That(youtubeSeconds).IsEqualTo(ExpectedOneMinFifteenSec);
        await Assert.That(youtubeListEmpty).IsTrue();
        await Assert.That(zeroDuration).IsTrue();
        await Assert.That(zeroSec).IsEqualTo(-1);
        await Assert.That(badUnitSec).IsEqualTo(-1);
        await Assert.That(shortsMatched).IsTrue();
        await Assert.That(bilibiliBadPage).IsTrue();
        await Assert.That(biliPageEmpty).IsTrue();
        await Assert.That(indexerTrailingPath).IsTrue();
        await Assert.That(twitchHtml).Contains("parent=already.example.com");
        await Assert.That(twitchHtml).DoesNotContain("localhost");
        await Assert.That(twitchFallbackHtml).Contains("parent=localhost");
        await Assert.That(nonNoCookiePlaylistHtml).Contains("https://www.youtube.com/embed/videoseries?list=PL123");
    }

    /// <summary>Emits a YouTube playlist with <see cref="VideoOptions.YouTubePrivacyEnhanced"/> disabled.</summary>
    /// <param name="url">Input playlist URL.</param>
    /// <returns>Emitted HTML string.</returns>
    private static string EmitNonPrivacyPlaylist(ReadOnlySpan<byte> url)
    {
        _ = VideoUrlParser.TryParse(url, VideoProviders.YouTube, out var match);
        ArrayBufferWriter<byte> writer = new();
        VideoEmbedEmitter.Emit(match, default, VideoOptions.Default.WithYouTubePrivacyEnhanced(false), default, writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    /// <summary>Parses <paramref name="url"/> and emits HTML with an empty resolved Twitch parent to exercise the <c>localhost</c> fallback.</summary>
    /// <param name="url">Input Twitch URL.</param>
    /// <returns>Emitted HTML string.</returns>
    private static string EmitWithEmptyParent(ReadOnlySpan<byte> url)
    {
        _ = VideoUrlParser.TryParse(url, VideoProviders.Twitch, out var match);
        ArrayBufferWriter<byte> writer = new();
        VideoEmbedEmitter.Emit(match, default, VideoOptions.Default, default, writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    /// <summary>Runs probe, parse, and emit synchronously so <c>ref struct</c> slices never cross an <c>await</c> boundary.</summary>
    /// <param name="inputUrl">Input video URL.</param>
    /// <returns>Tuple of probe result, parse result, and emitted HTML.</returns>
    private static (bool Probed, bool Parsed, string Html) ParseAndEmit(string inputUrl)
    {
        var urlBytes = Encoding.UTF8.GetBytes(inputUrl);
        var probed = VideoUrlProbe.MayContainEmbeddableUrl(urlBytes, VideoProviders.All);
        var parsed = VideoUrlParser.TryParse(urlBytes, VideoProviders.All, out var match);
        if (!parsed)
        {
            return (probed, false, string.Empty);
        }

        ArrayBufferWriter<byte> writer = new();
        VideoEmbedEmitter.Emit(match, default, VideoOptions.Default, "docs.example.com"u8, writer);
        return (probed, true, Encoding.UTF8.GetString(writer.WrittenSpan));
    }

    /// <summary>Parameterized test record pairing a supported input URL with expected CSS modifier and <c>src</c> substring.</summary>
    /// <param name="InputUrl">Input video URL.</param>
    /// <param name="ExpectedModifier">Expected BEM modifier class.</param>
    /// <param name="ExpectedSrc">Expected substring inside emitted HTML.</param>
    [System.Diagnostics.DebuggerDisplay("{InputUrl}")]
    public readonly record struct VideoUrlCase(string InputUrl, string ExpectedModifier, string ExpectedSrc);
}
