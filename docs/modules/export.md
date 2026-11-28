# Single-File Print & PDF Export (`NuStreamDocs.Export`)

`NuStreamDocs.Export` harvests every rendered documentation page during the build and emits a standalone, self-contained HTML5 handbook (`/print.html` by default) ready for browser **Print -> Save as PDF**, headless Chromium PDF generation, or offline reading.

## Key Capabilities

- **Deterministic ordering**: Places `index.md` first, followed by all site chapters in deterministic path order.
- **In-document link rewriting**: Rewrites internal cross-page `href` links to `#nd-page-...` chapter anchors so links navigate cleanly inside the single HTML file or exported PDF.
- **Print & `@page` CSS**: Embeds `@page` margins, chapter page breaks (`break-before: page`), `break-inside: avoid` rules on code blocks/tables/figures, and hides interactive chrome when printing.

## Installation

```bash
dotnet add package NuStreamDocs.Export
```

## Quick Start

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Export;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseExport(options => options
        .WithDocumentTitle("NuStreamDocs User Guide"u8)
        .WithSubtitle("Print & Offline Edition"u8))
    .BuildAsync();
```

## Options (`ExportOptions`)

| Property | Default | Description |
|---|---|---|
| `OutputFileName` | `"print.html"` | Relative output path under `WithOutput(...)`. |
| `DocumentTitle` | `"Documentation"u8` | Cover `<h1>` and HTML `<title>`. |
| `Subtitle` | `[]` | Optional cover subtitle rendered beneath the heading. |
| `IncludeTableOfContents` | `true` | Renders a linked chapter `<ol>` table of contents after the cover header. |
