# Blogs & RSS/Atom Feeds

**Packages**: `NuStreamDocs.Blog`, `NuStreamDocs.Blog.MkDocs`, `NuStreamDocs.Blog.Common`, `NuStreamDocs.Feed`

## Two Blog Flavors

1. **`NuStreamDocs.Blog.MkDocs` (`.UseMkDocsBlog()`)**:
   Follows `mkdocs-material` conventions: reads posts under `blog/posts/` with `date:`, `categories:`, and `authors:` YAML frontmatter, generating paginated blog index and category archive pages.
2. **`NuStreamDocs.Blog` (`.UseWyamBlog()`)**:
   Follows Wyam/Statiq conventions: reads `YYYY-MM-DD-slug.md` files with `Title`, `Published`, `Tags`, and `Author` frontmatter, generating index and tag archive pages.

## Pairing with `NuStreamDocs.Feed`

`NuStreamDocs.Feed` reuses the shared blog post scanner to write `rss.xml` (RSS 2.0) and `atom.xml` (Atom 1.0) syndication feeds to the site root:

```csharp
using NuStreamDocs.Blog.MkDocs;
using NuStreamDocs.Common;
using NuStreamDocs.Feed;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterial3Theme()
    .UseMkDocsBlog()
    .UseFeed(new FeedOptions(
        siteUrl: [.. "https://docs.example.com"u8],
        title: [.. "Engineering Blog"u8],
        description: [.. "Release announcements and deep dives."u8],
        postsSubdirectory: (PathSegment)"blog/posts"))
    .BuildAsync();
```
