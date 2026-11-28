# AI & LLM Documentation Endpoints (`llms.txt`)

**Package**: `NuStreamDocs.Llms`

## What It Does

AI coding assistants, IDE chat tools, and search crawlers struggle with full HTML page shells (navigation drawers, script bundles, and theme chrome). Following the [`llms.txt`](https://llmstxt.org/) convention used across modern developer docs, `NuStreamDocs.Llms` generates plain UTF-8 Markdown endpoints alongside your rendered HTML site:

1. **`/llms.txt` (Site Directory)**: A concise Markdown index grouped by top-level folder, listing every page's title, canonical URL, and one-line summary so an assistant can pick which pages to read without downloading the entire site.
2. **`/llms-full.txt` (Full Reference Bundle)**: One single file containing every included page's clean Markdown body (with YAML front matter stripped and source URLs attached) for RAG ingestion or full-context prompts.
3. **Per-Page `.md` Siblings (`/guide/install.md`)**: Writes a clean `.md` copy next to each rendered page under `OutputRoot` so appending `.md` to a documentation path returns pure Markdown instead of HTML.
4. **HTML `<head>` Discovery Link**: Injects `<link rel="alternate" type="text/markdown" href="/llms.txt" title="LLMs Index">` into every page's `<head>`.

## Installation

```bash
dotnet add package NuStreamDocs.Llms
```

## Quick Start

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Llms;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteName("Acme SDK"u8)
    .WithSiteUrl("https://docs.acme.example"u8)
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseLlms(opts => opts
        .WithSiteSummary("High-throughput event streaming SDK for .NET 10 and .NET 11."u8))
    .BuildAsync();
```

## Concrete Input and Output Example

Given these two source files in `docs/`:

**`docs/index.md`**
```markdown
---
title: Welcome
description: Getting started with the Acme SDK.
---

Install the NuGet package and create a client.
```

**`docs/guide/install.md`**
```markdown
# Installation

Run `dotnet add package Acme.Sdk` in your project directory.
```

Running `BuildAsync()` produces:

### 1. `site/llms.txt`

```markdown
# Acme SDK

> High-throughput event streaming SDK for .NET 10 and .NET 11.

## Guide

- [Installation](https://docs.acme.example/guide/install/): Run `dotnet add package Acme.Sdk` in your project directory.

## Overview

- [Welcome](https://docs.acme.example/): Getting started with the Acme SDK.
```

### 2. `site/llms-full.txt`

```markdown
# Acme SDK

---

# Installation
Source: https://docs.acme.example/guide/install/

# Installation

Run `dotnet add package Acme.Sdk` in your project directory.

---

# Welcome
Source: https://docs.acme.example/

Install the NuGet package and create a client.
```

### 3. `site/guide/install.md` and `site/index.md`

Clean Markdown copies with YAML front matter stripped. Pages whose title came only from YAML front matter (`docs/index.md`) automatically receive a `# Welcome` H1 heading at the top of their `.md` sibling; pages that already start with `# Installation` keep their existing heading without duplication.

## How Metadata Is Extracted

- **Title**: Reads YAML front matter `title:` (or `Title:`), falls back to the first ATX `# Heading` outside fenced code blocks, and finally humanizes the file name stem (`api-ref/index.md` -> `Api ref`).
- **Summary**: Reads YAML front matter `description:` or `summary:`, falls back to the first plain paragraph line outside code fences/lists/tables/blockquotes, and truncates cleanly at `MaxSummaryBytes` (`160` bytes by default) without splitting multi-byte UTF-8 characters.
- **Section Heading**: Humanizes the top-level directory segment (`guide/install.md` -> `Guide`) or uses `DefaultSectionTitle` (`Overview` by default) for root-level files.

## Excluding Pages (`llms: false`)

Add `llms: false` to any page's YAML front matter to omit it from `llms.txt`, `llms-full.txt`, and `.md` sibling output while still rendering its normal HTML page:

```yaml
---
title: Legal Terms
llms: false
---
```

## Configuration Options (`LlmsOptions`)

| Option | Fluent Method | Default | Description |
|---|---|---|---|
| `EmitFullText` | `.WithEmitFullText(bool)` | `true` | Writes `site/llms-full.txt` concatenating all included pages. |
| `EmitMarkdownSiblings` | `.WithEmitMarkdownSiblings(bool)` | `true` | Writes clean `.md` copies into `OutputRoot` mirroring each page's relative path. |
| `EmitHeadLink` | `.WithEmitHeadLink(bool)` | `true` | Injects `<link rel="alternate" type="text/markdown" href="/llms.txt" title="LLMs Index">` into `<head>`. |
| `MaxSummaryBytes` | `.WithMaxSummaryBytes(int)` | `160` | Maximum UTF-8 byte length for summaries in `llms.txt` before appending `...`. |
| `SiteSummary` | `.WithSiteSummary("..."u8)` | `[]` | Optional `> ...` blockquote summary rendered below `# SiteName` in `llms.txt`. |
| `DefaultSectionTitle` | `.WithDefaultSectionTitle("..."u8)` | `"Overview"` | Section heading used in `llms.txt` for root-level Markdown files. |
