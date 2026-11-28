# API & Code Playgrounds (`NuStreamDocs.Playgrounds`)

`NuStreamDocs.Playgrounds` turns fenced `http-request` and `playground` blocks into interactive API consoles and lazy-loaded code sandboxes. It works both alongside `.UseSuperFences()` (via `ICustomFenceHandler`) and standalone via HTML post-rendering.

## Installation

```bash
dotnet add package NuStreamDocs.Playgrounds
```

## Quick Start

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Playgrounds;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UsePlaygrounds()
    .BuildAsync();
```

## Fenced Block Syntax

### 1. HTTP Request Console (`http-request`)

````markdown
```http-request
POST https://api.example.com/v1/items
Authorization: Bearer token123
Content-Type: application/json

"{\"name\":\"Widget\"}"
```
````

Renders an endpoint header badge, pure-CSS radio tabs for **cURL**, **C# (`HttpClient`)**, and **JavaScript (`fetch`)**, plus an optional collapsible **Try Request** drawer that executes browser `fetch` calls.

### 2. Live Code Sandboxes (`playground`)

````markdown
```playground
provider=dotnetfiddle
id=abc123
title=Try LINQ Query
```
````

Supported `provider` values: `dotnetfiddle` (default), `stackblitz`, `codepen`, `jsfiddle`, or pass `src=https://...` directly. Emits a `<figure>` with `<iframe loading="lazy" sandbox="allow-scripts allow-forms allow-same-origin">`.

## Options (`PlaygroundsOptions`)

| Property | Default | Description |
|---|---|---|
| `EnableInteractiveTryIt` | `true` | Appends the browser **Try Request** drawer and includes `nstd-playgrounds.js`. |
| `IncludeAssets` | `true` | Emits and links `nstd-playgrounds.css` (and `nstd-playgrounds.js` when `EnableInteractiveTryIt` is enabled). |
