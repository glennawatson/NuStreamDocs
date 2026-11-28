# Table of Contents & Tags

**Packages**: `NuStreamDocs.Toc`, `NuStreamDocs.Tags`

## `NuStreamDocs.Toc`

Scans rendered HTML headings (`<h2>` through `<h6>` by default), injects deterministic slug `id` attributes and permalink anchors (`¶`), and populates the theme's right-hand "On this page" sidebar (`<!--@@toc@@-->`).

### Usage & Options

```csharp
using NuStreamDocs.Toc;

builder.UseToc(new TocOptions(
    MinLevel: 2,
    MaxLevel: 4,
    PermalinkSymbol: "#",
    MarkerSubstitute: true));
```

| Option | Default | Description |
|---|---|---|
| `MinLevel` | `2` | Shallowest heading level included in the TOC sidebar (`h1` is skipped by default). |
| `MaxLevel` | `6` | Deepest heading level included in the TOC sidebar. |
| `PermalinkSymbol` | `"¶"` | Text inside the `<a class="headerlink">` anchor appended to each heading. |
| `MarkerSubstitute` | `true` | Replaces `<!--@@toc@@-->` in the page shell with the generated TOC markup. |

---

## `NuStreamDocs.Tags`

Reads `tags:` YAML frontmatter from every page and generates a searchable `/tags/` landing page plus individual tag listing pages.

### Frontmatter Syntax

Both inline flow lists and YAML block lists work:

```yaml
---
title: Zero-Allocation Span Parsing
tags: [performance, utf8, spans]
---
```

### Usage & Options

```csharp
using NuStreamDocs.Tags;

builder.UseTags(new TagsOptions(
    OutputSubdirectory: "tags",
    IndexFileName: "index.html"));
```
