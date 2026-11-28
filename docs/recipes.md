# Common Recipes

Copy-paste `DocBuilder` setups for common documentation architectures. See the [Module Index](index.md) for full option tables on each plugin.

## Documentation Site with C# API Reference

```csharp
using NuStreamDocs.Autorefs;
using NuStreamDocs.Building;
using NuStreamDocs.CSharpApiGenerator;
using NuStreamDocs.Highlight;
using NuStreamDocs.LinkValidator;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Nav;
using NuStreamDocs.Search.Sqlite;
using NuStreamDocs.Theme.Material3;
using NuStreamDocs.Toc;
using NuStreamDocs.Xrefs;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteUrl("https://docs.example.com"u8)
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseNav(opts => opts with { Prune = true })
    .UseToc()
    .UseHighlight()
    .UseSqliteSearch()
    .UseAutorefs()
    .UseXrefs()
    .UseCSharpApiGenerator(
        CSharpApiGeneratorOptions.FromPackages(
            [new NuGetPackageReference("MyLib", "1.0.0")],
            apiCachePath: ".api-cache"))
    .UseCommonMarkdownExtensions()
    .UseLinkValidator(opts => opts with { Strict = true })
    .BuildAsync();
```

## Privacy-Hardened, Self-Hosted Site

Downloads external images/stylesheets at build time, self-hosts `.woff2` fonts, hashes inline scripts into a strict Content Security Policy, minifies HTML, and pre-compresses `.gz` and `.br` files:

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Csp;
using NuStreamDocs.Fonts;
using NuStreamDocs.Optimize;
using NuStreamDocs.Privacy;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterial3Theme()
    .UseFonts(Material3Fonts.Default)
    .UsePrivacy()
    .UseCsp()
    .UseHtmlMinify()
    .UseOptimize()
    .BuildAsync();
```

## Blog with RSS 2.0 and Atom Feeds

```csharp
using NuStreamDocs.Blog.MkDocs;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Feed;
using NuStreamDocs.Nav;
using NuStreamDocs.Theme.Material;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme()
    .UseNav()
    .UseMkDocsBlog()
    .UseFeed(new FeedOptions(
        siteUrl: [.. "https://docs.example.com"u8],
        title: [.. "Engineering Blog"u8],
        description: [.. "Latest releases and notes."u8],
        postsSubdirectory: (PathSegment)"blog/posts"))
    .BuildAsync();
```

## Multi-Release Versioned Docs (`versions.json`)

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Theme.Material3;
using NuStreamDocs.Versions;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput($"site/{version}")
    .UseMaterial3Theme()
    .UseVersions(opts => opts
        .WithManifest("site/versions.json")
        .WithVersion(version)
        .WithAliases("latest"))
    .BuildAsync();
```

## Live-Reload Local Development Server

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Highlight;
using NuStreamDocs.Nav;
using NuStreamDocs.Serve;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterial3Theme()
    .UseNav()
    .UseHighlight()
    .WatchAndServeAsync(opts => opts with { OpenBrowser = true });
```
