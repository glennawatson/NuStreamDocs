# Bibliography & Citations

**Package**: `NuStreamDocs.Bibliography`

## What It Does

Resolves Pandoc-style citation markers (`[@key]`, `[@key, p 23]`, `[@first; @second]`) against a `BibliographyDatabase`, emits numbered citation footnote references inline, and appends a formatted `## Bibliography` section to each citing page. Ships **AGLC4** (Australian Guide to Legal Citation, 4th edition) out of the box and loads standard CSL-JSON exports from Zotero or Pandoc.

## Fluent Database Registration

```csharp
using NuStreamDocs.Bibliography;
using NuStreamDocs.Bibliography.Model;

builder.UseBibliography(db => db
    .AddCase("mabo", "Mabo v Queensland (No 2)", "(1992) 175 CLR 1", 1992)
    .AddBook(
        "gummow",
        "Change and Continuity",
        PersonName.Of("William", "Gummow"),
        2018,
        "Federation Press"));
```

## Loading from CSL-JSON (`references.json`)

```csharp
using NuStreamDocs.Bibliography;
using NuStreamDocs.Bibliography.Csl;

var entries = CslJsonLoader.LoadFile("references.json");
var dbBuilder = new BibliographyDatabaseBuilder();
for (var i = 0; i < entries.Count; i++)
{
    dbBuilder.Add(entries[i]);
}

builder.UseBibliography(new BibliographyOptions(
    dbBuilder.Build(),
    Aglc4Style.Instance,
    WarnOnMissing: true));
```

## Markdown Syntax

```markdown
Indigenous land title was recognized in *Mabo* [@mabo] and analyzed further in later commentary [@gummow, p 42].
```
