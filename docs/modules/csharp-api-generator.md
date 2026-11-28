# C# API Reference Generator

**Package**: `NuStreamDocs.CSharpApiGenerator`

## What It Does

Wraps `SourceDocParser` to extract public types, methods, properties, events, generic constraints, and XML documentation comments from .NET assemblies or NuGet packages, emitting structured Zensical-compatible API reference pages plus a namespace index landing page.

## Four Input Shapes

```csharp
using NuStreamDocs.CSharpApiGenerator;

// 1. From nuget-packages.json manifest at the repo root:
builder.UseCSharpApiGenerator(
    CSharpApiGeneratorOptions.FromManifest(
        rootDirectory: ".",
        apiCachePath: ".api-cache"));

// 2. From explicit NuGet package IDs/versions:
builder.UseCSharpApiGenerator(
    CSharpApiGeneratorOptions.FromPackages(
        [new NuGetPackageReference("MyCompany.Core", "2.1.0")],
        apiCachePath: ".api-cache"));

// 3. From local compiled DLLs:
builder.UseCSharpApiGenerator(
    CSharpApiGeneratorOptions.FromAssemblies(
        tfm: "net11.0",
        assemblyPaths: ["src/MyLib/bin/Release/net11.0/MyLib.dll"]));
```

## Options (`CSharpApiGeneratorOptions`)

| Option | Default | Description |
|---|---|---|
| `OutputMarkdownSubdirectory` | `"api"` | Subdirectory under `docs/` (or synthetic route prefix in direct mode) where API pages land. |
| `Mode` | `EmitMarkdown` | Writes `.md` files or streams synthetic pages directly (`UseCSharpApiGeneratorDirect`). |
| `EmitIndexPage` | `true` | Generates `api/index.md` listing every documented namespace. |
| `IndexTitle` | `"API Reference"` | Custom UTF-8 heading for the API landing page. |
| `IndexIntroduction` | `null` | Optional UTF-8 introductory paragraph above the namespace table. |
| `IndexOrder` | `null` | Optional integer `Order:` frontmatter value for sidebar placement. |
