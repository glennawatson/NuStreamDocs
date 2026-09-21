# Markdown rendering: where we differ from MkDocs and Zensical

NuStreamDocs renders basic Markdown to HTML in a single UTF-8 pass. For the large
majority of documents the output is the same as MkDocs (Python-Markdown) and
Zensical. This page lists the places where it is not, and why.

Every difference falls into one of three groups:

- **[CommonMark behavior we keep](#commonmark-behavior-we-keep).** The references
  differ from CommonMark and we follow CommonMark, or we make a small deliberate
  output choice.
- **[Reference behavior we do not copy](#reference-behavior-we-do-not-copy).** The
  references produce output that is a defect or a side effect of how they process
  text, and we render the intended result.
- **[Reference features outside basic Markdown](#reference-features-outside-basic-markdown).**
  Extensions that Zensical enables by default and that are site-build concerns, not
  Markdown syntax.

CSS classes, theme markup and highlighter wrappers are not compared. NuStreamDocs
uses its own Material 3 theme.

## How the comparison works

Zensical is the primary reference. MkDocs is the reference where Zensical is buggy
or lacks the behavior. A clear bug in either one is never copied.

The test project `src/tests/NuStreamDocs.ParityCheck.Tests` keeps this honest. It
renders a corpus of Markdown fragments with the renderer and with both reference
engines, and classifies each fragment:

| Result | Meaning |
|---|---|
| `PASS` | Our output equals Zensical's. |
| `EXPECTED` | We differ on purpose. A sidecar file records the reason and the exact output we pin. |
| `EXTENSION` | We differ because Zensical applies an extension that basic Markdown does not. |
| `MKDOCS-BETTER` | Zensical has a defect that MkDocs does not; we match MkDocs. |
| `DIFF` | An unexplained difference. The check fails. |

Each difference on this page is pinned by a fragment or a regression test. A change
that moves one fails a test.

## CommonMark behavior we keep

### Lists

**A list item's body is indented to the item's content column.** Python-Markdown
requires four spaces to nest a list or hold a code block inside an item. We accept
the indent that the item's marker implies, so two spaces after `- ` and three after
`1. ` both work.

```markdown
- parent one
  - child one
  - child two
- parent two
```

```html
<ul>
<li>parent one
<ul>
<li>child one</li>
<li>child two</li>
</ul>
</li>
<li>parent two</li>
</ul>
```

**A list can start directly after a paragraph line.** No blank line is needed.

```markdown
A paragraph line
- list item directly after it
```

**Ordered lists keep their start number.** `5.` produces `<ol start="5">`, and `0.`
produces `<ol start="0">`.

**A change of marker kind starts a new list.** A bullet list followed by an ordered
list, or the reverse, is two lists.

**A list is loose as a whole.** If any item is separated from the next by a blank
line, every item is wrapped in a paragraph. The references decide item by item.

```markdown
- one
- two

- three
```

```html
<ul>
<li>
<p>one</p>
</li>
<li>
<p>two</p>
</li>
<li>
<p>three</p>
</li>
</ul>
```

**`1)` starts an ordered list, and a lone marker is an empty item.** A line that
holds only `-`, `*`, `+` or `1.` is an empty list item.

### Headings

**`#hashtag` is text.** An ATX heading needs a space after the `#` run.

**ATX headings may be indented up to three spaces.** Setext headings may span
several lines.

### Block quotes and HTML

**Block quotes separated by a blank line are separate quotes.**

**An HTML block ends at the first blank line.** Markdown after the blank line is
parsed as Markdown again.

```markdown
<div>
first part

second part
</div>
```

```html
<div>
first part
<p>second part</p>
</div>
```

### Code

**An unclosed fence runs to the end of the document.** A fence is closed only by a
fence of its own character, so a `~~~` line inside a backtick fence is code.

**Text after the language on a fence line is kept.** It appears in a `data-info`
attribute of the code element, so tooling can read it.

````markdown
```js title="a.js" {1,3}
let a = 1;
```
````

```html
<pre><code class="language-js" data-info="title=&quot;a.js&quot; {1,3}">let a = 1;
</code></pre>
```

**Tabs inside fenced code are kept after the leading indentation.** Code is emitted
as written. Leading tabs expand to spaces.

### Inline text

**Every ASCII punctuation character can be backslash-escaped.** Python-Markdown
escapes only a fixed set.

**Email autolinks are plain `mailto:` links.** The references obfuscate the address
with character entities. The plain link is smaller and readable.

**Emphasis pairs by the CommonMark delimiter-run rules.** The closing run pairs with
the nearest opening run, runs of different lengths pair, and the multiple-of-three
rule applies. Underscores inside a word never open or close emphasis. Where
Zensical differs, CommonMark wins. For example, `**a*b**` renders as one strong span
around `a*b`, where Zensical renders `*<em>a<em>b</em></em>`.

**Emphasis nests at most 32 spans deep.** Markers beyond that stay literal. The cap
bounds the cost and stack use of pathological input.

### Links

**An inline link inside link text is literal.** `[a [l](m)](u)` renders as a link to
`u` whose text is `a [l](m)`. Zensical and MkDocs behave the same way. They still
nest in the cases where they nest: autolinks, images and reference links inside
link text, and links after another inline element or inside emphasis.

## Reference behavior we do not copy

Each of these is a defect or a side effect in Zensical, MkDocs or both. We render
what the syntax means.

### Headings

- **`####### text`** (seven hashes) is a paragraph. Python-Markdown renders an `h6`
  that starts with a stray `#`.
- **`# text # `** (a closing `#` run followed by a space) drops the closing run.
  Python-Markdown keeps it in the heading text.
- **A lone `-` line after `- one`** is an empty list item. Python-Markdown reads it
  as a setext underline that turns the previous line into an `h2`.

### Lists and quotes

- **A sibling item after a nested list** that follows a blank line stays a sibling of
  the parent item. Python-Markdown attaches it to the nested list.
- **A lone `>` line** is an empty block quote. Python-Markdown continues it lazily
  onto the next line.

### Code

- **A fence directly after a paragraph line** ends the paragraph. Zensical nests the
  `<pre>` inside the `<p>`, which is invalid HTML.
- **An empty fence** renders an empty code element. Zensical adds a newline.
- **Backtick runs of different lengths** do not pair. An unmatched run stays
  literal. Zensical pairs them and drops characters.
- **A lone backtick** with no closing run stays literal. Zensical wraps it in
  `<code>`.
- **Empty and whitespace-only code spans** pair only backtick runs of equal length.
  The references pair a two-backtick run with a one-backtick run.
- **A trailing tab** is not a hard break. Python-Markdown expands it into one.

### Links and references

- **`[a](http://example.com/"x")`** keeps the quoted string in the URL. Python-Markdown
  reads a quoted string glued to a bare URL as a link title.
- **Consecutive reference definitions** each resolve to their own target. The
  references resolve `[e]` to the next definition's target and lose one.
- **A malformed definition** such as `[also broken]: <unclosed` stays paragraph
  text. Python-Markdown swallows it together with the following line.
- **A collapsed or full reference inside a reference link's label**, as in
  `[a [r][] b][s]` and `[a [l][r] b][s]`, resolves as an ordinary reference. The
  references resolve only the shortcut form there, so `[r][]` leaves a stray `[]`
  and `[l][r]` leaves `[l]` as text followed by a link on `r`. The same forms resolve
  normally everywhere else, including inside an inline link's text.

## Reference features outside basic Markdown

Zensical enables extensions by default whose behavior belongs to a site build, not to
Markdown syntax. They are intentionally not part of the core renderer.

| Behavior | Zensical extension |
|---|---|
| Bare URLs, `www` hosts and email addresses become links, including URLs inside malformed link syntax. | magiclink |
| `\(` and `\)` keep their backslashes. | arithmatex |
| A backslash followed by a space is an escaped space, for example in a link destination. | subscript and superscript |
| Relative `.md` link targets are rewritten to directory URLs. | site link processing |

The optional NuStreamDocs plugins cover several of these as separate features. See
the [README](../README.md) for the plugin list.
