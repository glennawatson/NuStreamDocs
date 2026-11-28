# Content Loaders (`File`, `HTTP`, `Feed`, `GitHub`, `OpenAPI`)

**Packages**: `NuStreamDocs.ContentLoader`, `NuStreamDocs.ContentLoader.Feed`, `NuStreamDocs.ContentLoader.GitHub`, `NuStreamDocs.ContentLoader.OpenApi`

## What They Do

Content loaders synthesize pages from external or structured data sources during the `Discover` phase. Each synthesized page flows through the exact same Markdown, theme, search, navigation, and sitemap pipeline as local `.md` files without writing temporary files into your `docs/` folder.

## Registration

```csharp
using NuStreamDocs.ContentLoader;
using NuStreamDocs.ContentLoader.Feed;
using NuStreamDocs.ContentLoader.GitHub;
using NuStreamDocs.ContentLoader.OpenApi;

builder.UseContentLoaders(
    new FileContentLoader("data/plugins.json", mapping),
    new OpenApiContentLoader("specs/petstore.yaml", "api/rest"),
    new GitHubReleasesContentLoader("glennawatson", "NuStreamDocs", "changelog"));
```

## Built-In Loaders

| Loader | Package | Source Input | Output Pages |
|---|---|---|---|
| `FileContentLoader` | `NuStreamDocs.ContentLoader` | Local JSON or YAML array | One Markdown page per item using a route template, body key, and mapped frontmatter keys. |
| `HttpContentLoader` | `NuStreamDocs.ContentLoader` | Remote REST GET or GraphQL POST JSON endpoint | One Markdown page per returned record. |
| `RawDocumentContentLoader` | `NuStreamDocs.ContentLoader` | Fixed list of raw Markdown URLs | Fetches remote `.md` files verbatim into target routes. |
| `FeedContentLoader` | `NuStreamDocs.ContentLoader.Feed` | External RSS 2.0 or Atom XML feed | One page per feed entry with `title`, `date`, and `external_url` frontmatter. |
| `GitHubContentLoader` | `NuStreamDocs.ContentLoader.GitHub` | Folder inside a GitHub repo (`owner`, `repo`, `ref`, `path`) | Imports remote Markdown trees directly via the GitHub REST API. |
| `GitHubReleasesContentLoader` | `NuStreamDocs.ContentLoader.GitHub` | GitHub Releases API | Generates release-notes / changelog pages from published GitHub releases. |
| `OpenApiContentLoader` | `NuStreamDocs.ContentLoader.OpenApi` | OpenAPI 3.x JSON or YAML spec (file or URL) | Generates one reference page per OpenAPI tag with operation endpoints, parameter tables, request bodies, and response codes. |
