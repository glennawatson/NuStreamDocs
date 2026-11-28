# Embedded Videos

**Package**: `NuStreamDocs.Videos`

## What It Does

`NuStreamDocs.Videos` converts standalone video URLs and Markdown media links (`![Title](url)`) into responsive, accessible `<iframe>` or `<video>` players in a single UTF-8 byte pass with zero managed allocations on non-matching pages.

## Supported Video Platforms

`VideoProviders.All` enables all 14 built-in provider groups:

1. **YouTube** (`youtube.com/watch`, `youtu.be`, `youtube.com/shorts`, `youtube.com/live`, `youtube.com/playlist`, `youtube-nocookie.com` by default, plus `?t=1m30s` start times and 9:16 vertical container for Shorts).
2. **Vimeo** (`vimeo.com/{id}`, unlisted `vimeo.com/{id}/{hash}`, channel/showcase URLs, `player.vimeo.com`, `dnt=1` by default).
3. **OneDrive** (`onedrive.live.com/embed` and shared `resid` + `authkey` links).
4. **SharePoint & Microsoft Stream** (`*.sharepoint.com/:v:/...`, `_layouts/15/stream.aspx`, `_layouts/15/embed.aspx`, `web.microsoftstream.com/video/{id}`).
5. **Azure** (`videoindexer.ai`, `vi.azure.com`, `amp.azure.net`, and Azure Blob / CDN / Front Door `.mp4`/`.webm` streams).
6. **Loom** (`loom.com/share/{id}`, `loom.com/embed/{id}`).
7. **Dailymotion** (`dailymotion.com/video/{id}`, `dai.ly/{id}`, `geo.dailymotion.com`).
8. **Twitch** (`twitch.tv/videos/{id}`, channel clips, `clips.twitch.tv`, `player.twitch.tv`, resolving Twitch's required `parent` host from `SiteUrl` or `WithTwitchParent(...)`).
9. **Wistia** (`*.wistia.com/medias/{id}`, `fast.wistia.net/embed/iframe/{id}`).
10. **Cloudflare Stream** (`*.cloudflarestream.com/{id}`, `videodelivery.net/{id}`).
11. **Google Drive** (`drive.google.com/file/d/{id}/view` -> `/preview`).
12. **Bilibili** (`bilibili.com/video/BV...`, `av...`, `player.bilibili.com`).
13. **Vidyard** (`share.vidyard.com/watch/{id}`, `play.vidyard.com/{id}`).
14. **Direct HTML5 Video Files** (`.mp4`, `.webm`, `.ogv`, `.m4v`, `.mov` -> `<video controls preload="metadata">`).

## Registration & Options

```csharp
using NuStreamDocs.Videos;

builder
    .WithSiteUrl("https://docs.example.com"u8)
    .UseVideos(opts => opts
        .WithProviders(VideoProviders.All)
        .WithEmbedBareUrls(true)
        .WithEmbedImageLinks(true)
        .WithEmbedStandaloneLinks(false)
        .WithYouTubePrivacyEnhanced(true)
        .WithVimeoDoNotTrack(true)
        .WithLazyLoad(true)
        .WithIncludeStylesheet(true)
        .WithContainerClass("nd-video"u8)
        .WithTwitchParent("docs.example.com"u8));
```

## Authoring Syntax

```markdown
https://youtu.be/dQw4w9WgXcQ?t=45

![Architecture Walkthrough](https://www.loom.com/share/8a9b0c1d2e3f4a5b)

https://cdn.example.com/demos/quickstart.mp4
```

Fenced code blocks, tilde fences, 4-space indented code, inline code spans, raw HTML blocks, and inline prose `[watch here](url)` hyperlinks remain untouched.
