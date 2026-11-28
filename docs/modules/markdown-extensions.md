# Markdown Extensions

**Package**: `NuStreamDocs.MarkdownExtensions`

## What It Does

`NuStreamDocs.MarkdownExtensions` implements the block and inline Markdown extensions popularized by `pymdown-extensions` and `mkdocs-material`, all in single-pass UTF-8 without regular expressions.

## Enable Everything or Pick Individual Extensions

```csharp
using NuStreamDocs.MarkdownExtensions;

// Enable the full standard bundle:
builder.UseCommonMarkdownExtensions();

// Or enable individual extensions à la carte:
builder
    .UseAdmonitions()
    .UseTabs()
    .UseDetails()
    .UseFootnotes()
    .UseDefinitionLists()
    .UseAbbreviations()
    .UseAttrList()
    .UseTables()
    .UseCheckLists()
    .UseMark()
    .UseCaretTilde()
    .UseCriticMarkup()
    .UseInlineHilite()
    .UseMarkdownInHtml();
```

## Syntax Quick Reference

### Admonitions & Collapsible Details

```markdown
!!! note "Important Configuration"
    Indented body content rendered inside a styled callout box.

??? warning "Click to expand"
    Collapsed `<details>` callout (`???+` starts expanded).
```

### Content Tabs

```markdown
=== ".NET CLI"
    ```bash
    dotnet add package NuStreamDocs
    ```

=== "PackageReference"
    ```xml
    <PackageReference Include="NuStreamDocs" />
    ```
```

### Tables, Task Checklists, Footnotes & Abbreviations

```markdown
| Column | Alignment |
|:-------|----------:|
| Left   | Right     |

- [x] Completed item
- [ ] Pending item

Here is a statement with a footnote[^1] and TLS abbreviation.

[^1]: Footnote explanation rendered at the bottom of the page.
*[TLS]: Transport Layer Security
```

### Inline Formatting (`Mark`, `CaretTilde`, `CriticMarkup`, `AttrList`)

- `==highlighted text==` -> `<mark>highlighted text</mark>`
- `H~2~O` and `x^2^` -> `<sub>` and `<sup>`
- `{++added++}`, `{--deleted--}`, `{~~old~>new~~}` -> CriticMarkup `<ins>` / `<del>`
- `[Button](url){ .md-button .md-button--primary }` -> attaches classes/IDs/attributes
