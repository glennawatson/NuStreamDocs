# Schema Content Loaders (`NuStreamDocs.ContentLoader.Schemas`)

`NuStreamDocs.ContentLoader.Schemas` adds three `IContentLoader` implementations that turn non-REST schema specifications into structured Markdown reference pages in pure UTF-8:

- **`JsonSchemaContentLoader`**: Generates property tables (`Type`, `Required`, `Default`, `Allowed` enum values, `Description`) and `$defs` / `definitions` sections from **JSON Schema** (Draft-07 and 2020-12, in JSON or YAML).
- **`GraphQlSchemaContentLoader`**: Generates reference tables for `Query`, `Mutation`, `Subscription`, `type`, `input`, `interface`, and `enum` declarations (including `"""..."""` and `"..."` descriptions) from **GraphQL SDL** (`.graphql` / `.gql`).
- **`AsyncApiContentLoader`**: Generates channel and operation tables from **AsyncAPI 2.x and 3.x** event-driven specifications (in JSON or YAML).

## Installation

```bash
dotnet add package NuStreamDocs.ContentLoader
dotnet add package NuStreamDocs.ContentLoader.Schemas
```

## Quick Start

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.ContentLoader;
using NuStreamDocs.ContentLoader.Schemas;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseContentLoaders(
        new JsonSchemaContentLoader("schemas/config.schema.json", "reference/schemas"),
        new GraphQlSchemaContentLoader("schemas/api.graphql", "reference/graphql", "GraphQL API"),
        new AsyncApiContentLoader("schemas/events.asyncapi.yaml", "reference/events"))
    .BuildAsync();
```

## Loader Reference

| Loader | Supported Inputs | Generated Markdown Structure |
|---|---|---|
| `JsonSchemaContentLoader` | `.json`, `.yaml`, `.yml`, or `byte[]` | Frontmatter `title`, overview, root property table, and `## $defs` sub-schema tables. |
| `GraphQlSchemaContentLoader` | `.graphql`, `.gql`, or `byte[]` | Frontmatter `title`, plus `## TypeName (type\|input\|interface\|enum)` sections with field/argument/type or enum value tables. |
| `AsyncApiContentLoader` | `.json`, `.yaml`, `.yml`, or `byte[]` | Frontmatter `title`, version badge, `## Channels` table, and `## Operations` table. |

Each loader accepts either a local `FilePath` (resolved relative to `WithInput(...)`) or an in-memory UTF-8 `byte[]` buffer.
