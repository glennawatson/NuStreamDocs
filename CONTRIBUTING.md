# Contributing to NuStreamDocs

Thanks for your interest in contributing. This document is the
human-readable digest of how we build the project: coding style,
engineering rules, build/test commands. `CLAUDE.md` is the
companion machine-facing rule book — both must agree; if they
diverge, `CLAUDE.md` wins for hot-path detail and this doc wins
for narrative.

## Getting set up

Prerequisites: .NET 10 SDK (matched in `src/global.json`).

```sh
cd src
dotnet build NuStreamDocs.slnx
dotnet run --project tests/NuStreamDocs.Tests/NuStreamDocs.Tests.csproj
dotnet run --project tests/NuStreamDocs.Nav.Tests/NuStreamDocs.Nav.Tests.csproj
```

Tests use Microsoft Testing Platform + TUnit (configured in
`Directory.Build.props`); benchmarks use BenchmarkDotNet under
`benchmarks/`.

## Engineering rules

Code under `src/` (excluding `src/tests/**`) follows every rule below.
Test projects relax the allocation rules but keep the style rules.

### Architecture

- **AOT, no reflection in the hot path.** The production library has
  `IsAotCompatible=true`, `EnableTrimAnalyzer=true`,
  `EnableAotAnalyzer=true`. Reflection-using deps (BenchmarkDotNet) are
  isolated to their own assembly.
- **UTF-8 in, UTF-8 out.** `ReadOnlySpan<byte>` for input,
  `IBufferWriter<byte>` for output. No `string` materialization in
  parse, escape, or emit. UTF-8 string literals (`"..."u8`) for tag
  fragments and JSON property names.
- **Bounded caches only.** Anything cache-shaped is size and age capped
  and keyed by content hash so long-running watchers / large-project
  builds don't accrete memory.
- **Plugins via the AOT seam.** `DocBuilder.UsePlugin<T>()` with
  `where T : IDocPlugin, new()` compiles down to a direct
  `new T()` call. No `Activator.CreateInstance`, no assembly
  scanning. Plugin assemblies ship a `DocBuilder*Extensions`
  static class with `Use{Plugin}` extension methods.

### Async-first

- **Async-first, end to end.** Every contract that performs I/O —
  every plugin hook, every pipeline entry point, every public API
  that may touch the network or disk — is `Task` (or `ValueTask`
  when proven on a hot path). There is **no** sync-over-async in
  this repo: never `.GetAwaiter().GetResult()`, never `.Result`,
  never `.Wait()`. If a contract you control hands you a
  `CancellationToken`, you await — you do not bridge.
- **Sync impls return `ValueTask.CompletedTask` / `Task.CompletedTask`.**
  When an implementation has nothing async to do, the body is
  `=> ValueTask.CompletedTask` (or `Task.CompletedTask` when the
  contract is `Task`). The contract stays async; per-call cost is
  one cached singleton, no state machine, no allocation.
- **`ValueTask` first when zero-alloc is proven; `Task` otherwise.**
  Default *return type* on a new contract is `ValueTask` whenever
  there is a credible argument that most implementations will
  complete synchronously and the call site multiplies — proven by
  the shape of the contract (e.g. plugin hooks: most plugins do
  nothing in most hooks; `OnRenderPage` runs per-page-per-plugin,
  so a per-call `Task` allocation would dominate). Use `Task` only
  when the path is genuinely async-dominant (network, disk I/O on
  every call) or cold (one call per build) — there the `Task`
  allocation cost is dwarfed by the work and the simpler semantics
  win. Obey the consume-once rule for `ValueTask`: never `await`
  the same instance twice, never store it in a field.
- **`ConfigureAwait(false)` on every library `await`.** No
  exceptions in `src/` outside test projects.
- **Cancellation flows through.** Async hooks accept a
  `CancellationToken` and pass it down — never swallow, never
  default to `CancellationToken.None` when a real one is in scope.
- **Don't pass a context by `in` when the method is `async`.**
  `async` methods can't take `in` parameters. Pass the
  `readonly record struct` context by value — it's a few
  pointer-sized fields, the copy is free, and the alternative
  (boxing through an interface or carrying a ref-state machine)
  is worse.

### Allocation discipline

- **`for` over `foreach`.** Indexed `for` over arrays / `Span<T>` /
  `ReadOnlySpan<T>` / `List<T>` / `ImmutableArray<T>`. `foreach` only
  when the type genuinely lacks an indexer (`HashSet<T>`,
  `IAsyncEnumerable<T>`).
- **Plain arrays over immutable collections** on hot paths. `T[]`
  exposed via a property the renderer treats as immutable by
  convention beats `ImmutableArray<T>`'s wrapping struct. Build the
  array through an `internal static` helper (e.g. `NavBuilder.ToArray`)
  that rents from `ArrayPool<T>` where the upper bound is known.
- **Collection expressions `[..]` first; never `.ToArray()`.** When
  materializing into a final `T[]` / span / immutable array, write
  `[..source]`, `[a, b, ..tail]`, `[]` and let the compiler pick the
  layout — it's cheaper than `.ToArray()` (no boxed enumerator on
  `IEnumerable<T>`, optimal pool/array choice, span paths inlined).
  This applies to *every* shape: `ConcurrentQueue<T>` / `Concurrent
  Dictionary<K,V>` snapshots (`[.. queue]`, `[.. dict]`), buffer
  writers (`[.. sink.WrittenSpan]`), `MemoryStream` outputs, etc.
  `.ToArray()` is allowed only when there is no other path
  (genuinely rare).
- **`"..."u8` is a `ReadOnlySpan<byte>` over a static blob — keep it
  there.** UTF-8 string literals don't allocate at use site; the
  bytes live in the assembly's RVA section. Don't `.ToArray()` them
  (forces a heap copy) and don't write them into a `MemoryStream`
  just to fish out a `byte[]`. If a downstream API really needs
  `byte[]`, use `[.. "..."u8]` so the compiler stages the
  static-to-array path; otherwise expose the literal directly via a
  `static ReadOnlySpan<byte> X => "..."u8;` property and consume it
  as a span.
- **`List<T>` only when the final size is unknown.** Always pass a
  `capacity` to the constructor (`new List<T>(expectedCount)`); never
  capacity-less.
- **Concrete collection types in production APIs.** Take and return
  `Dictionary<K,V>` / `HashSet<T>` / `T[]` / `ReadOnlyDictionary<K,V>`
  / `ReadOnlySpan<T>` directly. **Never** `IDictionary<K,V>`,
  `IReadOnlyDictionary<K,V>`, `IList<T>`, `IReadOnlyList<T>`,
  `ICollection<T>`, `IReadOnlyCollection<T>`. Abstract collection
  interfaces add a vtable indirection on every member access, hide
  the storage shape from callers, and invite passing arbitrary
  impls that may not match our perf assumptions. The concrete type
  *is* the contract. Read-only intent: use the concrete
  `ReadOnlyDictionary<K,V>` wrapper or expose `T[]` (the renderer
  treats arrays as immutable by convention). `IEnumerable<T>` (or
  `IAsyncEnumerable<T>`) is allowed *only* when a streaming
  yield-based shape genuinely avoids loading the entire input into
  memory — large-file scans, unbounded walks, on-demand sources.
  Don't use `IEnumerable<T>` as a generic "any collection"
  parameter; reach for `T[]` / `ReadOnlySpan<T>` there.
- **`FrozenDictionary` / `FrozenSet` only when all four hold.** The
  freeze pass is *expensive at build time*; it pays back only when
  the table is then read many times. Reach for `Frozen*` only when
  *all* of these are true:
    1. **Built once or very rarely** — startup, configure, finalize;
       not on the per-page hot path.
    2. **Queried many times afterward** — across pages or across
       parallel workers.
    3. **Read-only after construction** — no mutation, no growth.
    4. **The lookup is meaningfully hot or broadly shared** — i.e.
       the table participates in a path where the constant-time
       improvement over `Dictionary<,>` is observable.
  If any of those is false — and especially if the collection is
  small, per-instance, or short-lived (per-render `TemplateData`,
  per-plugin small option sets) — use a plain `Dictionary<,>` /
  `HashSet<>` with the right comparer. The freeze cost will dominate
  the work the table actually does.
- **Pre-baked UTF-8 byte arrays** for per-level tag emission instead
  of per-call construction.
- **Pool transient buffers.** `ArrayPool<T>.Shared.Rent` paired with a
  `try`/`finally` `Return`. `PageBuilderPool` for the markdown-emit
  hot path.
- **Never `stackalloc` inside a loop.** The stack frame is shared
  across iterations, so per-iteration `stackalloc byte[N]` *accumulates*
  — `N * iterations` bytes, then reclaimed only when the method
  returns. On a hot path with thousands of iterations or a variable
  size, that's a stack-overflow waiting to happen. Hoist a single
  `stackalloc` (sized to the loop's maximum) **above** the loop and
  slice it per iteration with `buffer[..n]`. If the upper bound isn't
  known at the call site or can exceed the stack budget (~1 KB),
  rent from `ArrayPool<T>.Shared` once outside the loop instead.
  Stackalloc inside a method that's *called from* a loop is fine —
  each call gets its own frame and unwinds on return; the bug is only
  when the stackalloc and the loop share a frame.

### Style

- **US English in identifiers and prose.** Type names, member names,
  XML docs, comments, log/diagnostic messages, and commit messages
  use US spelling. Examples: `Normalize` (not the UK `-ise` form),
  `Serialize`, `Initialize`, `Color` (not `Color`), `Behavior`
  (not `Behavior`). The `.editorconfig`,
  analyzer rule names, and the BCL itself are US English; mixing
  dialects splits casing across tooling search.
- **`var` for locals; targeted `new()` for typed slots.** Always
  `var x = ...` for locals; never repeat the type on both sides
  (`Foo x = new Foo()` is banned — use `var x = new Foo()` or
  `Foo x = new(...)` depending on which side the type already lives
  on).
- **C# 14 `field` keyword by default** for properties that need extra
  logic (lazy init, validation, change-tracking). Use an explicit
  backing field only for `ref`-passing APIs (`Interlocked`,
  `Volatile`, `Unsafe.As`), constructor bypass, or when storage is
  referenced outside the property's accessors. When you do, leave a
  one-line `<remarks>` explaining why.
- **No top-level statements.** Every executable project defines a
  proper `public static class Program` with `Main` in its own file.
- **Most methods static.** Inner-layer scanners / emitters / helpers
  are `static`. Reserve instance methods for the outer-layer façade
  types that hold genuine per-instance state (`DocBuilder`, plugin
  instances). Static-only classes get `static class` declarations.
- **No default parameter values.** Use explicit overloads. Defaults
  bake into every caller's IL and obscure refactors.
- **`internal static` helpers** for stateless cross-type utilities.
  Group by responsibility, not by feature. Keep the public surface
  narrow.
- **Don't reach for `StringBuilder` when the destination is a UTF-8
  byte sink.** When the output ultimately ends up in an
  `IBufferWriter<byte>`, a `Stream`, or a file, write UTF-8 directly:
  `"..."u8` literals for static fragments, plus
  `Encoding.UTF8.GetBytes(string, Span<byte>)` straight into a span
  obtained from `writer.GetSpan(...)`. `StringBuilder` followed by
  `Encoding.UTF8.GetBytes(sb.ToString())` round-trips through UTF-16
  and allocates a string per page — a real cost on thousands of
  pages, and avoidable. Reserve `StringBuilder` for outputs that
  genuinely need a `string` (test assertions, error messages, log
  lines).
- **Avoid `while (true)`.** Every loop must have its termination
  condition expressed in the loop header. Rewrite as `while
  (cond)`, `do/while`, or a `for` with the bound spelled out — the
  reader (and the JIT) shouldn't have to scan the body for the
  break to know when the loop ends. The exception is genuinely
  infinite work (a server's accept loop, a pump that exits via an
  external signal), and even those should be a `while
  (!cancellationToken.IsCancellationRequested)`-shaped guard.
- **Bundle long parameter lists into a `readonly record struct` or
  `ref struct` rather than splitting the method.** When a single
  helper needs more than a handful of parameters and the parameters
  travel together (e.g. a render-state bundle of scope stack +
  iteration frames + cursor), define a record-shaped state type and
  pass it `in` (read-only) or `ref` (mutated). The state type
  documents the relationship between the values, lets the JIT keep
  them in registers, and avoids weakening the method by sprinkling
  fields onto the enclosing class. Don't reach for a `class` for
  this — it adds a heap allocation and obscures the lifetime.
- **One helper class per file, methods on it `public`.** When a
  feature splits into multiple cooperating helpers (the inline parser
  is the canonical example: `InlineRenderer`, `InlineEscape`,
  `CodeSpan`, `Emphasis`, `LinkSpan`, `AutoLink`, `HardBreak`), put
  each `internal static` class in its own file. Methods on those
  classes are `public` rather than `internal`/`private` so the test
  project (and other helpers within the same assembly) can reach
  them without needing reflection or `[InternalsVisibleTo]` to a
  finer level. The class itself stays `internal` to keep the public
  API surface small.
- **Choose the most-pattern-matching control flow available.** Order
  of preference: switch expression → switch statement → list of
  `if` / `else if` chains. Reach for the next form only when the
  prior one cannot express the dispatch (mutating `ref`/`out`
  values, side-effects in branches, fall-through). The inline-byte
  dispatcher in `InlineRenderer.TryHandleSpecial` is the reference
  shape — a switch expression mapping marker bytes to handler calls.
- **Prefer newer C# syntax by default.** When a newer language form is
  available and doesn't introduce a perf or allocation penalty, use it.
  Do not keep older syntax out of habit. In this repo that means
  preferring collection expressions, `params` collections, primary
  constructors, list patterns, raw string literals, alias-any-type
  `using` aliases, `System.Threading.Lock`, the `field` keyword, and
  other newer syntax whenever the generated code is as good or better.
- **Assume the newer form is clearer unless proven otherwise.** The bar
  for staying with older syntax is not "this is familiar"; it's "the
  newer form materially hurts the generated code or makes the intent
  harder to follow in this specific case".
- **Collection expressions first.** Prefer `[]`, `[a, b]`, and
  `[..source]` over `new[] { ... }`, ad-hoc array construction, and
  `.ToArray()` snapshots. This is the default syntax for final
  materialization, static tables, span/array literals, and collection
  snapshots.
- **Prefer `params` collections for fan-in APIs.** For APIs whose job is
  "accept several values and iterate once" — builder helpers, plugin
  registration, include/exclude patterns, asset registration, and
  factory helpers — prefer modern `params` collection forms when they
  don't add overhead.
- **Prefer primary constructors when they remove pure constructor
  boilerplate.** Good candidates are plugins, wrappers, registries, and
  other outer-layer types whose constructors mostly validate inputs and
  capture immutable dependencies or option objects. Keep explicit
  constructor bodies when they preserve a useful overload shape or need
  substantial setup logic.
- **Use `System.Threading.Lock` as the default synchronous lock
  primitive.** When new code genuinely needs a private monitor-style
  gate around shared mutable state, prefer `private readonly Lock _gate
  = new();` and `lock (_gate)`. Choose a different primitive only when
  the problem is actually atomic arithmetic, async coordination, or
  another non-monitor case.
- **Use the `field` keyword when a property needs logic; don't invent
  properties just to use it.** If state is naturally a field, keep it a
  field. If a property needs validation, lazy init, or change-tracking,
  prefer `field` over a hand-written backing field unless one of the
  repo's explicit exceptions applies.
- **One type per file.** Splits matching the StyleCop default keep
  greps fast and diffs small.

### Analyzers and suppressions

- **Fix the code, don't silence the rule.** The analyzer set
  (StyleCop, Roslynator, SonarAnalyzer, .NET CA, Blazor.Common)
  catches real perf and correctness issues; suppressing is the last
  resort.
- When suppression is genuinely correct, attach a per-symbol
  `[SuppressMessage("Category", "RuleId", Justification = "...")]`
  with a real reason. Project-wide `<NoWarn>` is acceptable only for
  bulk patterns scoped to a project (e.g. `CA1812` across an entire
  test project) and must carry a comment in the `.csproj` explaining
  the scope.

### Dependencies

- **Pin the latest non-beta version** when adding to
  `Directory.Packages.props`. Check
  `https://api.nuget.org/v3-flatcontainer/<lower-cased-id>/index.json`
  for the highest stable release; never a `-preview` / `-rc` /
  `-alpha` / `-beta`. Same rule for bumps.
- **Every production assembly is AOT-compatible.** New `*.csproj`
  files inherit `IsAotCompatible=true`, `EnableTrimAnalyzer=true`,
  `EnableAotAnalyzer=true` from `Directory.Build.props`. If you add
  a project that genuinely needs reflection (BenchmarkDotNet host,
  one-off tooling), set the three properties to `false` explicitly
  in that project's `.csproj` and document the reason in a comment.
  Never silently drop AOT compatibility from a library — the rest of
  the renderer relies on every assembly it loads being trim/AOT
  clean.
- Plugin assemblies own their dependencies. The `NuStreamDocs` core
  must not transitively pull in heavy deps that only one plugin
  needs.

## C# style guide

Baseline is "Visual Studio defaults plus the rules above". The rules in
the [Engineering rules](#engineering-rules) section take precedence
when this list and that list disagree (e.g. our project bans default
parameter values; the general C# guide allows them).

- **Allman braces.** Each `{` on its own line. A single-line statement
  block may go without braces only when the block is properly indented
  on its own line and not nested in another braced statement; the one
  exception is `using` statements that nest directly without braces.
- **Four-space indentation, no tabs.**
- **`_camelCase`** for `internal` / `private` instance fields; mark them
  `readonly` whenever possible. `static readonly` (in that order) for
  static fields. Public fields are rare and use `PascalCase` with no
  prefix.
- **Avoid `this.`** unless absolutely necessary.
- **Always specify visibility**, even when it's the default
  (`private string _foo`, never bare `string _foo`). Visibility comes
  first in the modifier list (`public abstract`, not
  `abstract public`).
- **Usings outside the namespace.** System namespaces first
  (alphabetical), then third-party (alphabetical). Promote to a
  `global using` only when a prefix appears in nearly every file in
  the project.
- **File-scoped namespaces** (`namespace Foo;`).
- **One blank line at most** between members. Never two consecutive
  blank lines.
- **No spurious whitespace** inside parens or before commas.
- **Existing-file style wins.** If you're editing a file whose style
  already differs (e.g. `m_foo` instead of `_foo`), match the file
  rather than rewriting it in the same PR.
- **`var` is encouraged** when the right-hand side makes the type
  obvious or the explicit type would be noise. Use the explicit type
  when it adds clarity (rare).
- **Language keywords over BCL types** (`int`, `string`, `float`,
  `int.Parse`) — both for declarations and for static-method calls.
- **`PascalCase` for constants** (`const`, `static readonly` value
  fields, and `private const` locals).
- **`nameof(...)`** instead of literal strings for member references
  whenever possible.
- **Fields at the top of the type declaration**, then constructors,
  then properties, then methods. StyleCop enforces this order.
- **Non-ASCII chars as Unicode escapes** (`\uXXXX`) rather than
  literal characters. Tools and editors occasionally garble literal
  glyphs.
- **Indent `goto` labels one level less** than the surrounding code.
- **XML doc comments on every publicly exposed member** — including
  `protected` members of public types. StyleCop enforces this.
- **Method groups** (`list.ForEach(Console.WriteLine)`) are encouraged
  over equivalent lambdas.
- **Expression-bodied members whenever possible.** Single-expression
  methods, properties, indexers, operators, and constructors collapse
  to `=>`-form. Reserve a block body for genuinely multi-statement
  members. The same applies to lambdas — `x => x + 1`, not
  `x => { return x + 1; }`.
- **Modern pattern matching everywhere.** Type / property /
  positional / list / relational / recursive patterns.
  `member is IMethodSymbol { IsExtensionMethod: true }` reads better
  than the equivalent if-chain.
- **Inline `out` variables** with `out var x`.
- **Deconstruction wherever it reads better.** Tuple returns
  (`var (start, length) = TrimRange(...)`), records
  (`var (op, target) = instr;`), `KeyValuePair` (`var (key, value) = pair;`)
  and any custom `Deconstruct(out ...)`. One named slot per piece of
  data is clearer than `result.Item1` / repeated property access.
- **Value tuples must have logical, concise names.** When declaring a
  tuple type or returning one (`(int Start, int Length)`,
  `(string Path, byte[] Bytes)`), name every element with a short
  noun — never lean on `Item1` / `Item2`. Same when destructuring a
  tuple parameter or local: pick names that read at the call site
  (`var (key, items) = pair;`).
- **Nullable reference types are on** project-wide; honor the
  warnings.
- **Range / index expressions** (`x[..n]`, `x[^1]`) for slicing.
  `Substring`, `Skip`, `Take` are last-resort.
- **`using` declarations** instead of nested `using` blocks where the
  scope reaches the end of the enclosing block.
- **Static local functions** when a local function captures nothing.
  Marks intent and stops accidental closure capture.
- **Switch expressions over statements** for value-based decisions.
- **`record` / `record struct`** for data-centric types with value
  semantics. `readonly record struct` is the default for small
  immutable shapes.
- **`init`-only setters** for properties that should only be set
  during initialization when a record is the wrong shape.
- **Target-typed `new()`** when the variable's type is already on the
  left (`Foo f = new(args)`); `var f = new Foo(args)` when the type
  reads from the right.
- **Static lambdas / anonymous functions** (`static (x) => ...`) when
  no capture is needed; lets the JIT skip the closure.
- **`with`-expressions** for non-destructive mutation of records and
  structs.
- **Raw string literals** (`""" ... """`) for multi-line / regex /
  JSON content; never escape-soup.
- **`required` modifier** on members that must be set during
  initialization.
- **Primary constructors** for outer-layer façade types where the
  parameters become fields directly used in the body. Avoid them on
  hot-path types where storage layout matters or you need explicit
  field modifiers.
- **Collection expressions** (`[a, b, ..tail]`, `[]`) over
  `new[]` / `new List<T>()`.
- The project bans **default parameter values** even though general
  C# allows them — see the engineering rules above.
- The project bans **default-valued lambda parameters** in production
  code for the same reason; tests may use them where it improves
  readability.

The repository ships an `.editorconfig` that enforces most of the
above. If a rule fires you don't expect, the rule wins — don't
suppress it without a documented Justification.

## Commit style

We follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/)
so the `git log` is mechanically scannable and tools (release-notes
generators, bots) can group changes by intent.

### Format

```
<type>(<optional scope>): <subject>

<body>

<footers>
```

### Types

| Type | When to use |
|---|---|
| `feat` | A new user-visible feature (a new plugin, a new builder method, etc.). |
| `fix` | A bug fix. |
| `perf` | A change that improves performance — typically backed by a benchmark number in the body. |
| `refactor` | An internal restructure that doesn't change behavior. |
| `docs` | README / CONTRIBUTING / xmldoc / inline-comment changes. |
| `test` | Adding or fixing tests, with no production-code change. |
| `build` | Changes to the build system, MSBuild props, NuGet packaging, embedded resources. |
| `ci` | Changes to GitHub Actions / Dependabot / SonarCloud config. |
| `chore` | Anything that doesn't fit above (lockfile bumps, repo housekeeping). |
| `revert` | Reverting an earlier commit. |

### Scope

Scope is the affected assembly or feature, lowercase, no `NuStreamDocs.`
prefix. Examples: `nav`, `theme.material`, `bibliography`,
`icons.materialdesign`, `markdown-extensions`, `cli`, `serve`. Use
`build` (no scope) for cross-cutting build infra. Omit the scope when
the change spans many assemblies in roughly equal measure.

### Subject

- ~70 characters max, imperative mood (`add`, `fix`, `cut`), lowercase
  initial letter.
- Don't end with a period.
- A single-sentence summary of *what changed and why it matters*.

### Body

- Explains the *why*, not the *what* (the diff shows the what).
- Wraps at ~80 chars.
- For `perf` commits, include the benchmark numbers (before / after,
  scenario, allocation delta) so reviewers and the future-you can
  trust the win is real.
- Describe constraints, alternatives considered, follow-ups.

### Footers

- `BREAKING CHANGE: <text>` (or `!` after the type — e.g. `feat(nav)!:`)
  for any change that alters a public API.
- Reference task numbers from the project's issue tracker
  (`Closes #123`, `Refs #456`).

### Examples

```
feat(bibliography): add AGLC4 citation style + pandoc marker resolver

Resolves [@key] / [@key, p 23] / [@a; @b] markers through a fluent
BibliographyDatabase or a CSL-JSON loader. Emits one numbered footnote
per citation plus a per-page `## Bibliography` section.

Closes #142
```

```
perf(nav): cut WithNav rxui-corpus alloc 229 MB → 74 MB (-68%)

Folds Path.GetRelativePath + Replace('\\', '/') into one string.Create
and skips the Microsoft.Extensions.FileSystemGlobbing matcher when no
glob filters were configured (the common case). Verified on the rxui
corpus benchmark: 544 ms → 390 ms.

Refs #210
```

```
refactor(markdown-extensions): centralize marker probes in
NuStreamDocs.Markdown.Common.MarkdownMarkerProbes

Was: each plugin had its own IndexOf("..."u8) NeedsRewrite override.
Now: shared helpers per marker family. Avoids the marker bytes
duplicating across plugins and gives one place to update if a syntax
spelling changes upstream.
```

## Tests and benchmarks

- TUnit + Verify under `src/tests/`. Treat tests as documentation —
  the names and asserts should communicate the contract.
- BenchmarkDotNet under `src/benchmarks/`. Always include
  `[MemoryDiagnoser]`; track allocations alongside throughput.
- Add a benchmark when you add a hot-path feature; add a regression
  test when you fix a bug.

## What goes where

- **Core library** (`src/NuStreamDocs/`) — parser, emitter, pipeline,
  plugin contract, mkdocs config reader. AOT-clean, no plugin
  dependencies.
- **Plugin assemblies** (`src/NuStreamDocs.Nav/`, future
  `src/NuStreamDocs.ApiGenerator/`, etc.) — one assembly per major
  feature. Each ships a `Use{Plugin}` extension method on
  `DocBuilder`.
- **CLI** (`src/NuStreamDocs.Cli/`) — `PublishAot=true` driver. Owns
  argument parsing and process exit codes; delegates all real work to
  `DocBuilder`.

## Reporting issues / proposing features

Open a GitHub issue with a minimal repro for bugs; for features,
describe the use case and any prior-art (mkdocs / Zensical /
mkdocs-material plugin equivalent) before sending a PR.
