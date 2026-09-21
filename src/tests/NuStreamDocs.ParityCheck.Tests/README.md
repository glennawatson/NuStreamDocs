# NuStreamDocs.ParityCheck.Tests

Compares `NuStreamDocs.MarkdownRenderer` with Zensical (primary reference) and MkDocs (Python-Markdown) on a corpus of Markdown fragments.

## Reference policy

- Zensical is the primary reference. A fragment passes when our output equals Zensical's.
- Where Zensical and MkDocs differ, we follow Zensical.
- Where Zensical is buggy or does not implement a basic-Markdown behavior, MkDocs is the reference for that behavior. Record it with a `mkdocs-better` sidecar. A clear bug in either reference is never copied.
- Zensical behavior that goes beyond basic Markdown is out of scope: magiclink bare-URL and email autolinks, arithmatex math, superfences extras, subscript/superscript escapes, `.md` link rewriting. Record it with an `extension` sidecar.
- CSS, class and theme markup is out of scope (normalized away, see "What counts as a difference").

## Tests

`dotnet test --solution NuStreamDocs.slnx` (or `--project tests/NuStreamDocs.ParityCheck.Tests/NuStreamDocs.ParityCheck.Tests.csproj`) from `src/` runs three groups:

| test | needs Python | what it checks |
|---|---|---|
| `ReferenceParityTests` | yes | one case per fragment: its classification (see "Statuses") is not a failing one |
| `SidecarPinnedOutputTests` (generated) | no | one row per `deviation`, `extension` and `mkdocs-better` sidecar: rendering the fragment gives the pinned HTML |
| `PinnedOutputSyncTests` | no | every sidecar of those kinds has its generated row, and every row has its sidecar |

The reference engines render the whole corpus once per test session, one adapter process per engine.

### Python

The tests find an interpreter, build a virtual environment with the pinned reference packages, and reuse it while the installed versions equal the pins (`ReferencePins`: MkDocs 1.6.1, Zensical 0.0.63, Python-Markdown 3.10.3, pymdown-extensions 12.0.1).

- Candidates are probed with `--version`, no shell: `python3`, `python`, then `python3.14` down to `python3.10`; on Windows `py -3`, `python`, `python3`. The first one at 3.10 or newer (the floor of the pinned packages) is used.
- The environment is `artifacts/parity-venv` under the repository root, which is git-ignored. `NUSTREAMDOCS_PARITY_VENV` overrides the directory. The interpreter inside is `bin/python` on Linux and macOS and `Scripts\python.exe` on Windows.
- A lock file next to the environment serializes concurrent test processes.
- No suitable interpreter: the reference tests are skipped with a reason that lists every probed candidate. The pinned-output tests still run.
- An interpreter exists but the environment cannot be created, the packages cannot be installed, or an engine fails: the reference tests fail with the tool's own error output.

`reference_adapter.py` only reads JSON on standard input, calls the reference library and writes JSON; it is launched by the tests and the tool, never by hand.

## Maintenance tool

A single-file app shares the classification code with the tests. Run it from this directory:

```bash
dotnet run --file tools/Program.cs                          # render every fragment, print the report, exit 1 on failing fragments
dotnet run --file tools/Program.cs -- --filter lists/       # fragments whose id contains the text (repeatable, case-insensitive)
dotnet run --file tools/Program.cs -- --id lists/nested-two-space   # exact fragment id (repeatable)
dotnet run --file tools/Program.cs -- --list                # list fragment ids and their sidecar kinds without rendering
dotnet run --file tools/Program.cs -- --show-all            # print outputs for PASS and EXPECTED fragments too
dotnet run --file tools/Program.cs -- --json                # machine-readable results
dotnet run --file tools/Program.cs -- --fragments <dir>     # use another corpus directory
dotnet run --file tools/Program.cs -- --no-zensical         # skip the Zensical engine; MkDocs stands in as the reference
dotnet run --file tools/Program.cs -- --emit-tests lists    # TUnit [Arguments(...)] rows for a feature folder (or "all"), expected HTML from Zensical
dotnet run --file tools/Program.cs -- --emit-tests lists --reference mkdocs   # take expected HTML from MkDocs instead
dotnet run --file tools/Program.cs -- --pin <kind> "<reason>" --id <fragment id>...   # write the .expect sidecar for the fragments
dotnet run --file tools/Program.cs -- --emit-pinned         # regenerate SidecarPinnedOutputTests.g.cs from the sidecars
```

The tool references `NuStreamDocs.csproj` by path, so it renders with the current working tree.

`--emit-tests` expected HTML is the chosen reference's output (our own bytes where they match it). Fragments with a `mkdocs-better` or `extension` sidecar take the MkDocs output, and fragments with a pinned sidecar take the pinned HTML. Rows for fragments that still differ carry a `FAILS until the renderer matches the reference` comment.

`--emit-pinned` rewrites `SidecarPinnedOutputTests.g.cs`. Run it after adding, removing or changing a `deviation`, `extension` or `mkdocs-better` sidecar; `PinnedOutputSyncTests` fails until the rows match the sidecars. A row carries the sidecar's pinned HTML, or our current output when the sidecar has none.

## Fragments

`fragments/<feature>/<name>.md`, one fragment per file, the id is `<feature>/<name>`. Basic Markdown only.
Fragments render exactly as stored (tabs, trailing spaces and missing final newlines are significant).
A fragment whose file name starts with `crlf-` is stored with LF line endings, which the repository's line-ending normalization requires, and rendered with CRLF line endings.

An optional `<name>.expect` sidecar records why a fragment does not simply equal Zensical:

```
kind: deviation
reason: A list is loose as a whole, not item by item.
---
<the exact HTML our renderer produces>
```

The `---` line and the HTML after it are optional.

| kind | meaning | our output |
|---|---|---|
| `deviation` | documented in the README section "Core Markdown rendering" | not Zensical (the pinned HTML) |
| `undocumented` | ours follows CommonMark, the references differ, the README does not list it | not Zensical (the pinned HTML) |
| `quirk` | reference behavior that is not worth matching | not Zensical (the pinned HTML) |
| `known-bug` | known defect kept visible without failing the run | not Zensical (the pinned HTML) |
| `mkdocs-better` | Zensical is buggy or does not implement the behavior; MkDocs is right | MkDocs |
| `extension` | Zensical's output comes from an extension beyond basic Markdown | MkDocs |

Pin a sidecar with `--pin <kind> "<reason>" --id <fragment id>`, which writes the sidecar with our current output as the pinned HTML. Sidecars for `mkdocs-better` and `extension` may be written by hand without pinned HTML, since our output equals MkDocs.

A sidecar fails the run when it is out of date:

- `STALE-EXPECT`: our output now equals Zensical; delete the sidecar.
- `CHANGED`: our output differs from the pinned HTML.
- `DIFF` on a `mkdocs-better` / `extension` fragment: our output no longer equals MkDocs.

## Statuses

| status | meaning | fails the run |
|---|---|---|
| `PASS` | ours equals Zensical | no |
| `MKDOCS-ONLY` | ours equals MkDocs but not Zensical, no sidecar | yes |
| `DIFF` | ours equals neither reference, no sidecar | yes |
| `MKDOCS-BETTER` | ours equals MkDocs; a `mkdocs-better` sidecar records why MkDocs is right | no |
| `EXTENSION` | ours equals MkDocs; an `extension` sidecar records the out-of-scope Zensical extension | no |
| `EXPECTED` / `UNDOCUMENTED` / `KNOWN-BUG` | `deviation` / `quirk`, `undocumented`, `known-bug` sidecar with matching pinned output | no |
| `STALE-EXPECT`, `CHANGED`, `ERROR`, `NO-REFERENCE` | sidecar out of date, renderer threw, or no reference output | yes |

Where Zensical and MkDocs agree, `PASS` and `DIFF` are the only outcomes. With `--no-zensical`, MkDocs is the primary reference and `mkdocs-better` / `extension` sidecars are not checked for staleness.

## What counts as a difference

Both sides are normalized before comparing. These never count:

- trailing newline, whitespace between block tags, runs of spaces and tabs, indentation after a line break (except inside `<pre>`)
- `<br>` / `<br/>` / `<br />` and other void-element spelling, attribute order and quoting, entity spelling (named, decimal, hex)
- CSS and theme markup: `class` and `style` attributes, attribute-less `<span>` wrappers, highlighter `div.highlight` wrappers, code line spans and `__codelineno` anchors, `headerlink` permalink anchors
- `id` attributes on headings (added by the `toc` extension; the core renderer emits none)

## References

- Zensical: the real `zensical.config.parse_config` + `zensical.markdown.render.render` with an empty project config, so its default extension set applies (superfences, magiclink, betterem, smartsymbols, arithmatex, attr_list, inlinehilite, tilde, caret, and others). Those extensions change basic output, which is why `extension` sidecars exist.
- MkDocs: `MkDocsConfig` defaults (`toc`, `tables`, `fenced_code`) with Python-Markdown.
- The Zensical core is a Rust extension that the Python package imports; the installer takes the prebuilt wheel (`--only-binary=zensical`), so no Rust toolchain is needed.
