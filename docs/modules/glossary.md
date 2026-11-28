# Glossary & Term Auto-Linker (`NuStreamDocs.Glossary`)

`NuStreamDocs.Glossary` maintains a single source of truth for project terminology. During the build it:

1. Loads terms and definitions from `glossary.yml` / `glossary.json` in your docs input folder and/or programmatic `GlossaryOptions` entries.
2. Synthesizes an alphabetical `/glossary/` Markdown reference page (`# Glossary` with `## Term` anchor sections).
3. Wraps matching whole-word terms in rendered HTML prose with accessible `<abbr class="nd-glossary-term" tabindex="0" title="...">` elements and ships `/assets/stylesheets/nstd-glossary.css` for zero-JavaScript hover and keyboard-focus tooltips.

Headings (`<h1>`-`<h6>`), links (`<a>`), inline/block code (`<code>`, `<pre>`), scripts, styles, and existing `<abbr>` elements are never modified.

## Installation

```bash
dotnet add package NuStreamDocs.Glossary
```

## Quick Start

Create `docs/glossary.yml`:

```yaml
UTF-8: Variable-width character encoding capable of encoding all Unicode scalar values.
CLS: Cumulative Layout Shift, a Core Web Vital measuring visual stability.
```

Register `.UseGlossary()` on `DocBuilder`:

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Glossary;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseGlossary(options => options
        .AddTerm("SSG"u8, "Static site generator."u8))
    .BuildAsync();
```

## Options (`GlossaryOptions`)

| Property | Default | Description |
|---|---|---|
| `GlossaryFile` | `"glossary.yml"` | YAML map (`Term: Definition`) or JSON array (`[{"term":"...","definition":"..."}]`) resolved against `WithInput(...)`. |
| `OutputSubdirectory` | `"glossary"` | Subdirectory under which the synthetic `index.md` glossary page is emitted. |
| `PageTitle` | `"Glossary"u8` | Frontmatter and `<h1>` title of the generated glossary page. |
| `GenerateGlossaryPage` | `true` | Emits the central glossary page when at least one term is defined. |
| `AutoLinkTerms` | `true` | Wraps matching whole-word terms in `<abbr>` tags and links `nstd-glossary.css`. |
| `FirstOccurrenceOnly` | `true` | Wraps at most the first occurrence of each term on a given page. |
| `Terms` | `[]` | Programmatic `GlossaryEntry` items merged with file entries. |
