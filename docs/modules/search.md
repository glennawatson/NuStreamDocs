# Search (`Pagefind`, `Lunr`, `SQLite`)

**Packages**: `NuStreamDocs.Search`, `NuStreamDocs.Search.Pagefind`, `NuStreamDocs.Search.Lunr`, `NuStreamDocs.Search.Sqlite`

## Choosing a Search Engine

NuStreamDocs offers three build-time client-side search engines that plug into the same theme search dialog:

| Package | Builder Method | Best For |
|---|---|---|
| `NuStreamDocs.Search.Pagefind` | `.UsePagefindSearch()` | **General & medium-to-large sites**. Runs the bundled per-RID Pagefind binary at finalize time to emit sharded WASM inverted-index fragments with highlighted excerpts. |
| `NuStreamDocs.Search.Lunr` | `.UseLunrSearch()` | **Small-to-medium sites or pure managed environments**. Emits a Lunr-compatible JSON index with zero native binaries. |
| `NuStreamDocs.Search.Sqlite` | `.UseSqliteSearch()` | **Massive API reference sites (thousands of pages)** on hosts with file-count caps (such as Cloudflare Pages). Emits one `search/search.db` SQLite FTS5 file queried via HTTP Range requests (`sql.js-httpvfs`). |

## Usage Examples

### Pagefind Search

```csharp
using NuStreamDocs.Search.Pagefind;

builder.UsePagefindSearch(opts => opts
    .WithMinTokenLength(3)
    .WithSearchableFrontmatterKeys("summary"u8, "tags"u8)
    .WithSectionPriorities("guide/:100,api/:-50"u8)
    .WithStrictBinaryRequired(true));
```

### SQLite FTS5 Search

```csharp
using NuStreamDocs.Search.Sqlite;

builder.UseSqliteSearch(opts => opts
    .WithIndexFullBody(false)
    .WithExcludePathPrefixes("changelog/"u8)
    .WithSectionPriorities("guide/:80"u8));
```

### Lunr JSON Search

```csharp
using NuStreamDocs.Search.Lunr;

builder.UseLunrSearch(opts => opts
    .WithLanguage("en"u8)
    .WithSearchableFrontmatterKeys("tags"u8));
```

## Common Features Across All Three Engines

- **`SearchableFrontmatterKeys`**: Folds YAML frontmatter fields (such as `tags`, `author`, `summary`) into the searchable text of each page.
- **`SectionPriorities`**: Comma-separated `prefix:weight` rules (for example `"guide/:100,api/:-100"u8`) that boost conceptual guides above generated API pages in search results.
- **`MinTokenLength`**: Skips near-empty stub pages whose body text is shorter than the threshold.
