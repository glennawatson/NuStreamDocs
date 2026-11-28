# Syntax Highlighting & Custom Fences

**Packages**: `NuStreamDocs.Highlight`, `NuStreamDocs.SuperFences`

## `NuStreamDocs.Highlight`

Server-side, zero-JavaScript syntax highlighter powered by purpose-built UTF-8 lexers. Emits Pygments and `mkdocs-material` compatible `<div class="highlight">` markup, rich line-level code annotations, and `highlight.css` automatically.

### Installation & Setup

```bash
dotnet add package NuStreamDocs.Highlight
```

```csharp
using NuStreamDocs.Highlight;

builder.UseHighlight(opts => opts with
{
    CopyButton = true,
    LineNumbers = false,
    EnableAnnotations = true,
});
```

### Fence Attributes in Markdown

Add `title`, `linenums`, `hl_lines`, or `{...}` range tokens on any fenced code block:

````markdown
```csharp title="Program.cs" linenums="1" hl_lines="2 4-5"
using NuStreamDocs.Building;
var builder = new DocBuilder();
builder.WithInput("docs");
builder.WithOutput("site");
await builder.BuildAsync();
```
````

Brace shorthand (`{2,4-5}`) is also supported directly in the fence info string:

````markdown
```csharp {2,4-5}
```
````

Line numbers render via CSS `attr(data-line)` on `<span class="linenos" data-line="1"></span>`, so selecting or copying code never copies line numbers into the clipboard.

### Inline Diff & Focus Directives

Place `[!code ++]`, `[!code --]`, or `[!code focus]` inside a trailing `//` or `#` comment on any code line. The comment marker is stripped before lexing, and the rendered line receives the corresponding CSS modifier class:

````markdown
```csharp
public int Calculate(int input)
{
    return input + 1; // [!code --]
    return input * 2; // [!code ++]
}
```
````

When at least one line carries `[!code focus]`, the outer wrapper receives `<div class="highlight nd-has-focus">` so surrounding lines gently dim until the reader hovers over the block:

````markdown
```python
import os
token = os.environ["API_TOKEN"] # [!code focus]
print("ready")
```
````

### Numbered Code Callouts

Add `// (1)!` or `# (1)!` (`1` through `99`) at the end of a code line to render an inline numbered badge (`<span class="nd-code-callout" data-callout="1">1</span>`). When an ordered list immediately follows the code block, it is automatically styled with `class="nd-code-annotations"`:

````markdown
```csharp
var builder = new DocBuilder() // (1)!
    .UseDirectoryUrls();       // (2)!
```

1. Creates the streaming documentation pipeline.
2. Emits clean `/guide/setup/` directory paths.
````

### Options (`HighlightOptions`)

| Option | Default | Description |
|---|---|---|
| `WrapInHighlightDiv` | `true` | Wraps `<pre><code>` in `<div class="highlight">`. |
| `EmitTitleBar` | `true` | Emits `<span class="filename">...</span>` when `title="..."` is present on the fence line. |
| `CopyButton` | `false` | Emits a `<button class="md-clipboard md-icon">` copy button handled by theme JS. |
| `LineNumbers` | `false` | Emits `<span class="linenos" data-line="N">` on every code block even without `linenums="..."`. |
| `EnableAnnotations` | `true` | Processes inline `[!code ++]`, `[!code --]`, `[!code focus]`, and `(N)!` callout comments. |
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
