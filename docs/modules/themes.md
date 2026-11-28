# Themes (`Material` and `Material3`)

**Packages**: `NuStreamDocs.Theme.Material`, `NuStreamDocs.Theme.Material3`, `NuStreamDocs.Theme.Common`

## What They Do

Themes wrap each rendered HTML fragment into a complete, responsive documentation shell with a top header, collapsible left navigation drawer, right-hand table-of-contents sidebar, dark/light color-scheme toggle, search modal dialog, and embedded CSS/JS assets.

- **`NuStreamDocs.Theme.Material`**: Classic `mkdocs-material` layout and class structure.
- **`NuStreamDocs.Theme.Material3`**: Modern Material Design 3 token-driven surface (tonal elevation, shape tokens, and MD3 color roles).

All CSS and JavaScript assets are embedded inside the theme assembly, so Native AOT single-file executables need no external template directory at runtime.

## Installation

```bash
dotnet add package NuStreamDocs.Theme.Material3
# or
dotnet add package NuStreamDocs.Theme.Material
```

## Usage

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Icons.MaterialDesign;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterial3Theme(
        new MdiIconResolver(),
        opts => opts
            .WithSiteName("My Library"u8)
            .WithSiteUrl("https://docs.example.com"u8)
            .WithLogo("assets/logo.svg"u8)
            .WithRepoUrl("https://github.com/org/my-library"u8)
            .WithEditUri("https://github.com/org/my-library/edit/main/docs/"u8)
            .WithCopyright("Copyright 2026 My Project"u8))
    .BuildAsync();
```

## Theme Integration Contracts

Plugins integrate automatically with both themes without theme-specific coupling:

- **`IStaticAssetProvider`**: Declares `(FilePath Path, byte[] Bytes)` assets copied to the output directory once per build.
- **`IHeadExtraProvider`**: Appends `<link>`, `<style>`, or `<script>` tags into `<head>` with relative asset paths rewritten automatically for nested pages.
- **`<!--@@nav@@-->` and `<!--@@toc@@-->` markers**: Populated by `NuStreamDocs.Nav` and `NuStreamDocs.Toc`.
- **Icon shortcodes**: Passing `new MdiIconResolver()` enables `:material-*:` inline SVG icons in prose and navigation titles.
