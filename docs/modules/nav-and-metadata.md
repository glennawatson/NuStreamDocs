# Navigation & Page Metadata

**Packages**: `NuStreamDocs.Nav`, `NuStreamDocs.Metadata`

## `NuStreamDocs.Nav`

Builds the site-wide hierarchical sidebar navigation tree, resolves previous/next page footer links, warns on orphaned pages, and substitutes `<!--@@nav@@-->` inside the active theme.

### Installation & Setup

```bash
dotnet add package NuStreamDocs.Nav
dotnet add package NuStreamDocs.Metadata
```

```csharp
using NuStreamDocs.Metadata;
using NuStreamDocs.Nav;

builder
    .UseMetadata()
    .UseNav(opts => opts with
    {
        Prune = true,
        Excludes = ["drafts/**", "**/_internal/**"],
    });
```

### Controlling Order and Visibility

1. **Frontmatter Ordering (`order:` or `weight:`)**:
   ```yaml
   ---
   title: Getting Started
   order: 1
   ---
   ```
2. **Hiding Individual Pages (`not_in_nav` / `nav_exclude`)**:
   ```yaml
   ---
   title: Internal Runbook
   not_in_nav: true
   ---
   ```
   Hidden pages still render to HTML and remain reachable by URL, but do not appear in the sidebar.
3. **Directory `.pages` Files (`awesome-pages` compatible)**:
   Place `docs/guide/.pages` to rename a section, pin explicit page ordering, or hide an entire directory:
   ```yaml
   title: User Guide
   nav:
     - index.md
     - installation.md
     - ...
   ```
   Or suppress the entire folder from navigation:
   ```yaml
   hide: true
   ```

---

## `NuStreamDocs.Metadata`

Merges shared YAML frontmatter into pages before rendering, inspired by Statiq's directory and sidecar metadata cascade:

- **Directory defaults (`_meta.yml`)**: Inherited by every `.md` file in that folder and child folders unless overridden.
- **Page sidecar (`guide/intro.meta.yml`)**: Merged into `guide/intro.md`.
- **Inline frontmatter (`guide/intro.md`)**: Highest precedence; overrides both sidecar and `_meta.yml` keys.
