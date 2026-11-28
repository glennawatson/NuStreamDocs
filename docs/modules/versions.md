# Versioning (`versions.json`)

**Package**: `NuStreamDocs.Versions`

## What It Does

`NuStreamDocs.Versions` produces and updates a `mike`-compatible `versions.json` manifest at the root of a multi-version documentation deployment. Material and Material 3 themes read `versions.json` automatically to render a version switcher dropdown in the top header.

## Installation

```bash
dotnet add package NuStreamDocs.Versions
```

## Usage

When building release `2.1` into `site/2.1/`:

```csharp
using NuStreamDocs.Versions;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site/2.1")
    .UseMaterial3Theme()
    .UseVersions(opts => opts
        .WithManifest("site/versions.json")
        .WithVersion("2.1"u8)
        .WithTitle("2.1 (Stable)"u8)
        .WithAliases("latest"u8, "stable"u8))
    .BuildAsync();
```

## Emitted `versions.json` Structure

```json
[
  {
    "version": "2.1",
    "title": "2.1 (Stable)",
    "aliases": ["latest", "stable"]
  },
  {
    "version": "2.0",
    "title": "2.0",
    "aliases": []
  }
]
```

If an entry for `version` already exists in `versions.json`, `VersionsPlugin` updates it in place and strips any aliases moved to the newly published version so aliases such as `latest` remain unique.
