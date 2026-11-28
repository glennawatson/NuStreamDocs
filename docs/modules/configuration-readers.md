# Configuration Readers (`mkdocs.yml`, `zensical.toml`, `docfx.json`)

**Packages**: `NuStreamDocs.Config.MkDocs`, `NuStreamDocs.Config.Zensical`, `NuStreamDocs.Config.DocFx`

## What They Do

When migrating an existing documentation repository, configuration readers parse your existing declarative config file directly as UTF-8 bytes (no `YamlDotNet` or reflection) and translate its settings onto `DocBuilder` and `NavOptions`.

## Installation

```bash
dotnet add package NuStreamDocs.Config.MkDocs
dotnet add package NuStreamDocs.Config.Zensical
dotnet add package NuStreamDocs.Config.DocFx
```

## Usage

### MkDocs (`mkdocs.yml`)

Reads `site_name`, `site_url`, `site_author`, `docs_dir`, `site_dir`, `use_directory_urls`, and `nav:` hierarchies:

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Config.MkDocs;
using NuStreamDocs.Nav;
using NuStreamDocs.Theme.Material;

await new DocBuilder()
    .UseMkDocsConfig("mkdocs.yml")
    .UseMaterialTheme()
    .UseNav(opts => opts.WithMkDocsNav("mkdocs.yml"))
    .BuildAsync();
```

### Zensical (`zensical.toml`)

Reads Zensical TOML project settings and navigation definitions:

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Config.Zensical;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .UseZensicalConfig("zensical.toml")
    .UseMaterial3Theme()
    .BuildAsync();
```

### DocFX (`toc.yml` / `docfx.json`)

Imports DocFX table-of-contents trees (`name` / `href` / `items`) into `NuStreamDocs.Nav`:

```csharp
using NuStreamDocs.Config.DocFx;
using NuStreamDocs.Nav;

builder.UseNav(opts => opts.WithDocFxToc("docs/toc.yml"));
```
