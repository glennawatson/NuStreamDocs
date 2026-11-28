# Cross-References (`Autorefs`, `Xrefs`, `SphinxInventory`)

**Packages**: `NuStreamDocs.Autorefs`, `NuStreamDocs.Xrefs`, `NuStreamDocs.SphinxInventory`

## How They Work Together

1. **`NuStreamDocs.Autorefs`**: Collects every heading anchor ID and symbol UID across the site during the scan phase, then rewrites `@autoref:target-id` references to the resolved page URL and fragment during post-resolve.
2. **`NuStreamDocs.Xrefs`**: Emits a DocFX-compatible `xrefmap.json` at the site root so other .NET documentation sites can link to your API types, and imports external `xrefmap.json` files so `@xref:System.String` or external library UIDs resolve automatically.
3. **`NuStreamDocs.SphinxInventory`**: Compresses the shared `AutorefsRegistry` into a Sphinx v2 `objects.inv` file (zlib-compressed body) so Python/C++/Rust Sphinx sites using `intersphinx` can cross-link into your NuStreamDocs site.

## Installation

```bash
dotnet add package NuStreamDocs.Autorefs
dotnet add package NuStreamDocs.Xrefs
dotnet add package NuStreamDocs.SphinxInventory
```

## Full Cross-Site Setup

```csharp
using NuStreamDocs.Autorefs;
using NuStreamDocs.SphinxInventory;
using NuStreamDocs.Xrefs;

var registry = new AutorefsRegistry();

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteUrl("https://docs.example.com"u8)
    .UseAutorefs(registry)
    .UseXrefs(new XrefsOptions(
        OutputFileName: "xrefmap.json",
        BaseUrl: [.. "https://docs.example.com"u8],
        Imports: [],
        EmitMap: true))
    .UseSphinxInventory(registry, new SphinxInventoryOptions(
        ProjectName: "MyLibrary",
        Version: "2.0.0",
        OutputFileName: "objects.inv"))
    .BuildAsync();
```
