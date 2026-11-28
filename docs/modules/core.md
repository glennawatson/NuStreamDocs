# Core Engine & Plugin Pipeline

**Packages**: `NuStreamDocs`, `NuStreamDocs.Common`, `NuStreamDocs.Markdown.Common`, `NuStreamDocs.Templating`

## What It Does

`NuStreamDocs` is the zero-regex, UTF-8 byte-stream Markdown compiler and static-site build pipeline. Instead of installing a global CLI tool, you create a small console application, configure `DocBuilder`, and run `await builder.BuildAsync()`. Because every stage works on `ReadOnlySpan<byte>` and `IBufferWriter<byte>` without runtime reflection, your documentation generator publishes cleanly with Native AOT (`dotnet publish -p:PublishAot=true`).

## Installation

```bash
dotnet add package NuStreamDocs
```

## Configuring `DocBuilder`

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Theme.Material3;

var pageCount = await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteName("Acme SDK"u8)
    .WithSiteUrl("https://docs.acme.example"u8)
    .WithSiteAuthor("Acme Engineering"u8)
    .UseDirectoryUrls()
    .IncludeDrafts(false)
    .Include("**/*.md")
    .Exclude("**/_*.md")
    .UseMaterial3Theme()
    .BuildAsync();
```

### Core `DocBuilder` Methods

| Method | Default | Purpose |
|---|---|---|
| `.WithInput(dir)` | `./docs` | Root folder scanned for `.md` pages and static assets. |
| `.WithOutput(dir)` | `./site` | Destination folder where rendered HTML and static files are written. |
| `.WithSiteName("..."u8)` | empty | Site title shared with themes, feeds, and site emitters. |
| `.WithSiteUrl("..."u8)` | empty | Canonical base URL used by sitemaps, feeds, canonical links, and embeds. |
| `.WithSiteAuthor("..."u8)` | empty | Default site-wide author name. |
| `.UseDirectoryUrls()` | `false` | Emits `guide/intro/index.html` (`/guide/intro/`) instead of `guide/intro.html`. |
| `.IncludeDrafts(bool)` | `false` | Controls whether pages with `draft: true` frontmatter are rendered. |
| `.Include(glob)` / `.Exclude(glob)` | all `*.md` | Filters which source files participate in page discovery. |
| `.WithRocketLoaderOptOut(bool)` | `true` | Adds `data-cfasync="false"` to emitted `<script>` tags. |
| `.WithLogger(ILogger)` | `null` | Pipes structured build diagnostics to `Microsoft.Extensions.Logging`. |

## How the Build Pipeline Runs

Every `BuildAsync` call runs deterministic phases in order:

1. **Configure (`IBuildConfigurePlugin`)**: Plugins read `SiteUrl`, `SiteName`, `InputRoot`, `OutputRoot`, and register cross-page markers.
2. **Discover (`IBuildDiscoverPlugin`)**: Content loaders and blog/tag generators emit in-memory `SyntheticPage` items alongside disk `.md` files.
3. **Pre-Render (`IPagePreRenderPlugin`)**: Source-level Markdown rewriters (`Snippets`, `Macros`, `Videos`, `Metadata`) transform UTF-8 Markdown before HTML parsing.
4. **Markdown Render**: Single-pass UTF-8 CommonMark + enabled block/inline parser produces HTML.
5. **Scan (`IPageScanPlugin`)**: Read-only collectors (`Sitemap`, `Search`, `Autorefs`, `Tags`) inspect rendered HTML in parallel.
6. **Resolve (`IBuildResolvePlugin`)**: Cross-page registries freeze their lookup tables once all pages have been scanned.
7. **Post-Render & Post-Resolve (`IPagePostRenderPlugin`, `IPagePostResolvePlugin`)**: HTML transformers (`Highlight`, `Theme`, `Privacy`, `Csp`) wrap page shells and rewrite HTML.
8. **Finalize (`IBuildFinalizePlugin`)**: Site-wide artifacts (`sitemap.xml`, `robots.txt`, `search.db`, `.gz`/`.br` files, `Audit`) write to `OutputRoot`.

## Writing a Custom Plugin

Implement one or more phase interfaces from `NuStreamDocs.Plugins` and register the instance with `.UsePlugin(...)`:

```csharp
using System.Buffers;
using NuStreamDocs.Plugins;

public sealed class BannerPlugin : IPagePostRenderPlugin
{
    public ReadOnlySpan<byte> Name => "banner"u8;

    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    public bool NeedsRewrite(ReadOnlySpan<byte> html) => !html.IsEmpty;

    public void PostRender(in PagePostRenderContext context)
    {
        context.Output.Write("<aside class=\"notice\">Preview documentation</aside>\n"u8);
        context.Output.Write(context.Html);
    }
}
```
