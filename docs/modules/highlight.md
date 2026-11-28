# Syntax Highlighting & Custom Fences

**Packages**: `NuStreamDocs.Highlight`, `NuStreamDocs.SuperFences`

## `NuStreamDocs.Highlight`

Server-side, zero-JavaScript syntax highlighter powered by purpose-built UTF-8 lexers. Emits Pygments / `mkdocs-material` compatible `<div class="highlight">` markup and ships `highlight.css` automatically.

### Installation & Setup

```bash
dotnet add package NuStreamDocs.Highlight
```

```csharp
using NuStreamDocs.Highlight;

builder.UseHighlight(new HighlightOptions(
    ExtraLexers: [],
    WrapInHighlightDiv: true,
    EmitTitleBar: true,
    CopyButton: true,
    AutoDetectLanguage: false,
    DetectionLanguages: []));
```

### Fence Attributes in Markdown

Add `title="..."` on any fenced code block to render a filename header bar above the code:

````markdown
```csharp title="Program.cs"
var pages = await new DocBuilder().BuildAsync();
```
````

### Options

| Option | Default | Description |
|---|---|---|
| `WrapInHighlightDiv` | `true` | Wraps `<pre><code>` in `<div class="highlight">`. |
| `EmitTitleBar` | `true` | Emits `<span class="filename">...</span>` when `title="..."` is present on the fence line. |
| `CopyButton` | `false` | Emits a `<button class="md-clipboard md-icon">` copy button handled by theme JS. |
| `AutoDetectLanguage` | `false` | Scores unlabeled `<pre><code>` blocks heuristically and highlights high-confidence matches. |
| `DetectionLanguages` | `[]` | Restricts heuristic auto-detection to specific language aliases (empty allows all built-in lexers). |
| `ExtraLexers` | `[]` | Registers custom `Lexer` definitions alongside the built-in language catalogue. |

---

## `NuStreamDocs.SuperFences`

Dispatches fenced code blocks (` ```lang `) claimed by any registered `ICustomFenceHandler` plugin (such as `Mermaid` or custom diagram/widget plugins) so custom fence blocks are transformed into rich HTML before syntax highlighting runs.

```csharp
using NuStreamDocs.SuperFences;

builder.UseSuperFences();
```
