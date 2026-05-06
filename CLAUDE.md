# CLAUDE.md

## ⛔ STOP — DO NOT RUN DESTRUCTIVE GIT COMMANDS WITHOUT EXPLICIT PERMISSION ⛔

**`git checkout HEAD -- <path>`, `git checkout .`, `git restore`, `git reset --hard`, `git clean -fd`, `git stash drop`, `rm -rf` over a tracked path — every one of these silently destroys uncommitted working-tree changes that cannot be recovered through `git reflog`.**

**This means: if a file has unstaged edits — even ones the harness or a script applied a moment ago — running these commands erases them. There is no undo.**

The rule, in big bold letters because it has been broken before:

> ## **NEVER run a destructive git command on a path with uncommitted changes. CHECK first with `git status` / `git diff`. If anything is unstaged on that path, STOP and ASK the user before reverting, resetting, or cleaning. ALWAYS prefer fixing the file in place via the Edit tool over reverting.**

If you find a file in a broken state and your instinct is "let me just revert it" — that instinct is wrong when the changes are uncommitted. The correct moves, in order:

1. `git status --short <path>` — confirm whether the change is staged, unstaged, or both.
2. `git diff <path>` — see what's actually different.
3. **If the diff contains anything you didn't put there yourself** — the user, a linter, or any tool may have made intentional changes. **Ask before reverting.** Even if those changes look broken to you, they may be partially correct or in the middle of a multi-step transform.
4. **Fix the broken parts in place via Edit.** A surgical fix preserves the surrounding intentional changes; a checkout/restore destroys them.
5. Only after the user explicitly authorizes a revert: do it, scope it as narrowly as possible, and quote the exact command back to the user before running.

Same rule applies to `--no-verify`, `--force`, force-push, branch deletion, stash dropping, and any other action that throws away work. **Reversibility is the default; destruction needs explicit permission every single time.**

## Repository Orientation

- **Primary working directory for build/test:** `./src`
- **Main solution:** `src/NuStreamDocs.slnx`
- **Production libraries:**
  - `src/NuStreamDocs/` — Core markdown/render pipeline, template engine, plugin contracts, caching, and shared helpers. AOT-clean and dependency-light.
  - `src/NuStreamDocs.Config.MkDocs/` + `src/NuStreamDocs.Config.Zensical/` — config readers for mkdocs-style YAML and Zensical TOML shapes.
  - `src/NuStreamDocs.Theme.Material/` + `src/NuStreamDocs.Theme.Material3/` — embedded theme shells and static assets.
  - Feature/plugin assemblies live alongside the core (`NuStreamDocs.Nav`, `NuStreamDocs.Autorefs`, `NuStreamDocs.Search`, `NuStreamDocs.Privacy`, `NuStreamDocs.Blog`, `NuStreamDocs.Feed`, `NuStreamDocs.Highlight`, `NuStreamDocs.Mermaid`, `NuStreamDocs.Lightbox`, `NuStreamDocs.Optimize`, `NuStreamDocs.Versions`, icon packs, etc.).
- **Test projects:**
  - `src/tests/NuStreamDocs.Tests/` — core parser/emitter/pipeline unit tests.
  - Feature-specific tests sit beside their assemblies under `src/tests/` (`NuStreamDocs.Nav.Tests`, `NuStreamDocs.Privacy.Tests`, `NuStreamDocs.Theme.Material.Tests`, `NuStreamDocs.Theme.Material3.Tests`, config-reader tests, etc.).

## Build

```bash
# Restore + build (from src/)
cd src
dotnet build NuStreamDocs.slnx

# Build a single project
dotnet build NuStreamDocs/NuStreamDocs.csproj
```

`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` is set in `src/Directory.Build.props` for non-test projects, so `dotnet build` also writes NuGet packages to `artifacts/packages/`.

## Testing: Microsoft Testing Platform (MTP) + TUnit

This repo uses **Microsoft Testing Platform (MTP)** with **TUnit** (not VSTest).

- MTP is configured via `src/global.json` (`"runner": "Microsoft.Testing.Platform"`).
- `TestingPlatformDotnetTestSupport` is enabled in `src/Directory.Build.props`.
- `IsTestProject` is auto-detected via `$(MSBuildProjectName.Contains('Tests'))` in `Directory.Build.props`. Test projects automatically get `<OutputType>Exe</OutputType>`, the TUnit + Verify.TUnit packages, the implicit-usings switches, and `<NoWarn>$(NoWarn);CA1812</NoWarn>`.
- `IsPackable=false` is set on each test project explicitly (defence-in-depth — Directory.Build.props sets it too).

### Test Commands (run from `./src`)

**CRITICAL:** Run `dotnet test` from the `src/` directory so the `global.json` MTP runner config is discovered. Use `--project` to specify the test project.

```bash
cd src

# Core tests
dotnet run --project tests/NuStreamDocs.Tests/NuStreamDocs.Tests.csproj

# Nav tests
dotnet run --project tests/NuStreamDocs.Nav.Tests/NuStreamDocs.Nav.Tests.csproj

# Detailed output
dotnet test --project tests/NuStreamDocs.Tests/NuStreamDocs.Tests.csproj -- --output Detailed
```

### Testing Best Practices

- **Do NOT use `--no-build`** — always build before testing to avoid stale binaries.
- Use `--output Detailed` **before** `--` for verbose output.
- Run tests from `src/` so the repository `global.json` and `Directory.Build.props` settings are picked up.

### Code Coverage

Coverage uses **Microsoft.Testing.Extensions.CodeCoverage** wired in via `src/Directory.Build.props` (added to every test project). Per-assembly options (format, attribute exclusions) live in `src/testconfig.json` and are linked next to each test binary as `<AssemblyName>.testconfig.json`.

```bash
cd src

# Run core tests with coverage
dotnet test --project tests/NuStreamDocs.Tests/NuStreamDocs.Tests.csproj -- --coverage --coverage-output-format cobertura

# Generate an HTML report (install once: dotnet tool install -g dotnet-reportgenerator-globaltool)
reportgenerator \
  -reports:"**/TestResults/**/*.cobertura.xml" \
  -targetdir:/tmp/sourcedocparser_coverage \
  -reporttypes:"Html;TextSummary"
cat /tmp/sourcedocparser_coverage/Summary.txt
```

### Benchmarks

`src/benchmarks/SourceDocParser.Benchmarks/` is a BenchmarkDotNet harness covering `MetadataExtractor.RunAsync` end-to-end against the slim debug NuGet fixture (3 owner-discovered packages, 19 TFM groups). The global setup runs one full fetch to warm the local NuGet cache, so per-iteration timings measure the walk + merge + emit pipeline without the network leg.

```bash
cd src

# Run every benchmark in the assembly
dotnet run --project benchmarks/SourceDocParser.Benchmarks/SourceDocParser.Benchmarks.csproj --configuration Release

# Filter to a single benchmark via the BenchmarkDotNet switcher
dotnet run --project benchmarks/SourceDocParser.Benchmarks/SourceDocParser.Benchmarks.csproj --configuration Release -- --filter '*RunAsync*'
```

### Zensical render-smoke

This repository currently uses TUnit/MTP-focused project tests under `src/tests/`; keep new test projects aligned with that shape.

## Code Style

- `.editorconfig` at the repo root drives formatting + IDExxxx severities.
- StyleCop, Roslynator, and Blazor.Common analyzers are active in every project (configured in `src/Directory.Build.props`).
- `EnforceCodeStyleInBuild=true` so editorconfig severities for IDExxxx rules fire at compile time.
- File header copyright text comes from `stylecop.json` (`"companyName": "Glenn Watson and Contributors"`); SA1636 enforces every `.cs` file matches.
- Public APIs require XML documentation (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`); SA1600 / SA1611 / SA1615 catch missing element / parameter / return docs.
- Logging is source-generated `[LoggerMessage]` partial methods on `ILogger` parameters — no `logger.LogInformation("...")` direct calls (CA1848). Expensive argument expressions go behind `LogInvokerHelper.Invoke(...)` to gate evaluation on `IsEnabled`.
- Public concrete classes that have an interface counterpart (`MetadataExtractor`/`IMetadataExtractor`, `NuGetFetcher`/`INuGetFetcher`, `SourceLinkValidator`/`ISourceLinkValidator`) keep their private helpers + `[LoggerMessage]` partials `static`; only the public entry point is instance.
- **Check the common libraries before adding a new byte / UTF-8 / path / collection helper.** Most byte-level operations already exist in `NuStreamDocs.Common` (`AsciiByteHelpers` — `IsAsciiWhitespace`, `TrimAsciiWhitespace`, `SkipWhitespace`, `ToAsciiLowerByte`/`Char`, `ToLowerCaseInvariant`, `IsAsciiIdentifierByte`, `IsWordBoundary`, `RunLength`, `StartsWithIgnoreAsciiCase`, `AsciiCaseBit`; `ByteArrayComparer`, `ByteArrayCollectionExtensions`, `EmptyCollections`, the `Utf8*` family, `XmlEntityEscaper`, `HtmlSnapshotRewriter`, `PageBuilderPool`, the path/URL structs) or `NuStreamDocs.Markdown.Common` (`AsciiWordBoundary`, `CodeAwareRewriter`, `HtmlEntityDecoder`, `MarkdownCodeScanner`, `MarkdownMarkerProbes`, `ShortcodeScanner`, `TryRewriteAt`). Before writing a new `private static IsAsciiWhitespace` / `TrimAscii` / `ToLower` / "skip whitespace" / "find next special byte" helper, **grep the common library for an existing one and call it.** If the helper exists but is `private`, promote it (with proper docs + a one-line justification in the commit). If it doesn't exist and the operation is genuinely reusable (string-byte, UTF-8, ASCII case-fold, path manipulation), **add it to the appropriate common library** rather than inlining a duplicate in the consuming module — then have the consumer call it. Only keep a private helper when it encodes domain-specific behaviour no other module would want (e.g. CommonMark-link-label collapsed-space normalisation, plugin-specific marker shapes).
- **Path-typed public APIs use the strongly-named `NuStreamDocs.Common` wrappers, never `string`, `DirectoryInfo`/`FileInfo`, or `Nuke.Common.IO.AbsolutePath`.** Five `readonly record struct` types, each holding a single `string` field (same memory cost as a string, free `Equals`/`GetHashCode`, never touch the filesystem) with bidirectional implicit string conversion so call sites + BCL interop stay trivial:
  - **`DirectoryPath`** — absolute or rooted directory. Use `/` to join (`inputRoot / "guide"`).
  - **`FilePath`** — absolute or rooted file. `.Directory` / `.FileName` / `.Extension` / `.WithExtension(...)` for path manipulation.
  - **`PathSegment`** — relative subdirectory reference (e.g. `"api"`, `"blog/posts"`). Used for `*Subdirectory` option fields.
  - **`GlobPattern`** — file-system glob pattern (e.g. `"**/*.md"`). Used for include/exclude lists.
  - **`UrlPath`** — URL path (`/assets/foo.css`, `https://example.com/bar`). Distinct from disk paths even though both round-trip through forward-slashed strings. `.IsAbsolute` recognizes `http(s)://` and `//`.

  `DirectoryInfo`/`FileInfo` allocate heavier state and silently cache filesystem metadata — avoid them. Validate with the per-type `IsEmpty` check or `ArgumentException.ThrowIfNullOrEmpty(value.Value)` at API boundaries. Reach for `.Value` only when handing off to a Microsoft API that doesn't take the implicit conversion (e.g. `Path.Combine(dir.Value, file)`); the goal is type-safe paths everywhere except the immediate BCL interop surface.
- **`string` is banned in production code — public, internal, *and* private.** Every text parameter, field, property, return value, and local under `src/` (outside test projects) is one of: (a) `byte[]` / `ReadOnlySpan<byte>` / `ReadOnlyMemory<byte>` for UTF-8 text — the default for parse/render/emit; (b) one of the path/URL structs above (`FilePath`, `DirectoryPath`, `PathSegment`, `GlobPattern`, `UrlPath`); or (c) `NuStreamDocs.Common.ApiCompatString` — the explicit "this string only exists because an outside consumer demands it" marker, with implicit conversion to/from `string` so call sites stay trivial. `nameof(...)` is exempt because it's a compiler-emitted constant. Nothing else. **Private helpers count.** A `string` parameter on a `private static` method is the same architectural smell as a public one — the caller almost certainly already has bytes or a path struct, and the helper is silently down-converting. Take the bytes / the struct. When you touch a method that still uses `string`, convert it. When you write a new option-extension method (`WithSiteName`, `WithLogo`, …), provide `byte[]` and `ReadOnlySpan<byte>` overloads only — **no `string` overload** — so call sites use UTF-8 literals (`"..."u8`). Reach for `ApiCompatString` only when an outside contract genuinely forces a `string` across the boundary (config-file values, BCL signatures we cannot override, plugin hosts that hand us text); encode it to bytes once at construction and keep going.
- **US English in all identifiers, XML docs, comments, log/diagnostic messages, and commit messages** — use the US `-ize`/`-yze`/`-or` forms (`Normalize`/`Serialize`/`Initialize`/`Analyze`/`Color`/`Behavior`), never the UK `-ise`/`-yse`/`-our` forms. The `.editorconfig`, analyzer rule names, and the BCL itself are US English; mixing dialects produces inconsistent symbol casing in tooling search.
- **XML doc comments describe the contract, not the implementation.** Every public *and* internal type, method, property, parameter, and return gets a `<summary>` / `<param>` / `<returns>` that answers "what is this for?" — the consumer-facing contract. Implementation detail (which collection it wraps, which algorithm it uses, table sizes, GC behaviour, hashing strategy, whether something is `Frozen*` / pooled / cached, the data-structure layout, "encoded once at construction" / "stores X as Y" / "wraps Z" phrasings) does **not** belong in `<summary>` and rarely belongs anywhere. Wrong: `Byte-keyed icon lookup backed by a Dictionary<byte[], (int, int)> over a concatenated SVG blob with an IAlternateEqualityComparer for span lookups…`. Right: `Resolves a Material Design icon name to its SVG path-data bytes.` Same rule for methods (`Returns the local path for the URL.` not `xxHash3-keyed concurrent dictionary lookup, falling back to BuildLocalPath when the key is absent.`), properties (`Gets the page URL.` not `Returns the cached UTF-8 bytes lazily computed from _path.`), and `<param>` text (`Source strings.` not `Source strings; encoded once into byte[][] via Utf8Encoder.EncodeArray with the empty-array fast path.`).
- **`<remarks>` is reserved for genuinely confusing implementation detail** — a non-obvious algorithm, a workaround for a specific bug, a perf-driven choice that would surprise a reader. **Not** a place to dump every "stores X as Y" / "wraps Z" / "encoded once at construction" sentence — that's routine and the reader can see it in the code. Most types should have **no** `<remarks>` at all. If `<remarks>` would just paraphrase the field types or restate the obvious, delete it instead of writing it.
- **Multi-line XML doc content puts text between newline-delimited tags.** When a `<summary>` / `<remarks>` / `<param>` body needs more than one line, the opening and closing tags sit on their own lines and the content lines wrap between them — never let a single tag-and-content line stretch past 200 chars (S103 will trip), and never lean the opening tag on the first content line. Wrong: `/// <remarks>One long sentence … another sentence.</remarks>` (single line, blows past 200 chars) or `/// <remarks>First line` … `/// second line</remarks>` (open tag fused to content). Right: `/// <remarks>` on one line, content on its own line(s), `/// </remarks>` on its own line. Single-line summaries / params / returns stay single-line — only break onto multi-line form when the content actually wraps.

## Style guide reference

The full coding-style guide — Allman braces, `_camelCase` privates,
file-scoped namespaces, expression-bodied members, modern pattern
matching, every other Visual-Studio-default tweak — lives in
[CONTRIBUTING.md](CONTRIBUTING.md). Follow that document when writing
or editing code in this repo. The performance rules below extend
those style rules; when the two could disagree, the perf rule below
wins inside `src/NuStreamDocs/` and the project's plugin assemblies.

## Performance & Idiomatic C# Rules

These rules apply to **production code** (everything under `src/` that isn't a test project). Test projects (`src/tests/**`) are exempt from the allocation-discipline rules — `foreach`, LINQ, and `List<T>` are fine in tests where readability beats micro-optimization. The pattern-matching, switch-expression, and list-pattern rules still apply to tests because they're style-not-perf.

### Pattern matching & flow control

- **Invert `if`s to flatten the happy path.** Guard-clauses + early `return`/`continue` first; main logic stays unindented. No `else` clauses on guarded branches.
- **Switch expressions over `if`/`else` chains** — use property patterns (`{ IsPublic: true }`), positional patterns, and recursive patterns. Every `ModifierLabel`/`KindLabel` style helper should be a switch expression.
- **List patterns for emptiness/cardinality.** Prefer `is [_, ..]` over `.Length > 0` / `.Count > 0` / `!string.IsNullOrEmpty`. Prefer `is []` for empty. Use `is [var single]` to bind a single-element collection in one shot.
- **`is`/`is not` patterns over `==`/`!=`** for null and type checks. Combine type test + property check in one line: `member is IMethodSymbol { IsExtensionMethod: true }`.
- **`is 0` / `is { Length: 0 }`** etc. for property-pattern numeric and length checks where it reads cleanly.

### Allocation discipline

- **Zero-LINQ policy.** No `System.Linq` in production code. LINQ pulls in lambdas + iterators on every call. Use plain `for` loops.
- **Avoid `foreach` whenever a `for` loop with an indexer works.** `foreach` over `IEnumerable<T>` boxes/allocates an enumerator; even on `List<T>`/`Dictionary<T,U>` it allocates a struct enumerator the JIT often can't elide. Use `for (var i = 0; i < x.Length; i++)` on arrays / `Span<T>` / `ReadOnlySpan<T>`. Only use `foreach` when iterating a type that genuinely lacks an indexer (e.g. `HashSet<T>`) and you've considered materializing it to an array first.
- **Arrays over `List<T>`** when the final length is known up front. Pre-size and write by index. Reserve `List<T>` for genuinely unbounded growth, and pre-size with a capacity hint.
- **`Span<char>` / `ReadOnlySpan<char>` + range expressions** for prefix checks, slicing, parsing — never allocate a temporary `string` to call `.StartsWith` / `.Substring`.
- **UTF-8 string literals (`"..."u8`)** in JSON / byte-level parsing paths to skip the UTF-16 → UTF-8 round-trip. Default for `Utf8JsonReader` / `Utf8JsonWriter` property names and any byte-sequence comparisons.
- **Pre-size `StringBuilder` / `Dictionary` / `HashSet`** with a capacity hint that reflects the expected size. The integration tests catch cases where this matters.
- **Avoid `ImmutableArray<T>` / `ImmutableList<T>` on hot paths.** The wrapping struct adds an indirection on every read, the builder churns intermediate arrays, and Sonar/Roslynator hits don't outweigh a plain `T[]` exposed via a property the renderer treats as immutable by convention. Reach for an immutable collection only when the API is genuinely public and consumers must not mutate. Hide construction behind an `internal static` helper (e.g. `NavBuilder.ToArray`) so the pooling/sizing detail isn't duplicated at every call site.
- **`string.Create(length, state, span => ...)`** for short, hot-path string assembly when concatenation would otherwise build a tree of intermediates.

### Collection expressions & syntax

- **Collection expressions `[...]`** for arrays, lists, and search sets — `IndexOfAny(['/', '\\'])`, `[..]` for spread, etc. Lets the compiler pick the optimal layout.
- **Range expressions (`x[..n]`, `x[n..]`)** for slicing, never `Substring`.
- **`TryPop` / `TryDequeue` / `TryGetValue`** in loops — drop the redundant `Count > 0` / `ContainsKey` pre-check.

### Dependency hygiene

- **Pin the latest non-beta version when adding to `Directory.Packages.props`.** Before adding a `PackageVersion`, hit `https://api.nuget.org/v3-flatcontainer/<lower-cased-id>/index.json` (or `nuget search`) and pick the highest stable release — never a `*-preview`, `*-rc`, `*-alpha`, `*-beta`, or other pre-release tag. If a workload genuinely needs a pre-release feature, leave a comment on the `PackageVersion` line stating the gating capability and link to the tracking issue. Bumps to existing pins follow the same rule: same query, latest non-beta, no pre-releases.

### API shape

- **No default parameter values.** Provide explicit overloads instead. Default values bake the constant into every caller's call-site IL — bumping it later requires a recompile of every consumer, and combinations of defaults make refactors and analyzer fixes opaque. One overload per legal call shape; each one delegates to the most-specific overload that takes everything explicitly.

### Properties

- **C# 14 `field` keyword.** When you need a backing field with extra logic (lazy init, validation, change-tracking), use the contextual `field` keyword inside the property accessors instead of declaring a separate `_name` field. The compiler synthesizes the storage; the property remains the only public surface.

  **Valid reasons to keep an explicit backing field**:
  - **`ref`-passing APIs** — `Interlocked.Increment(ref _counter)`, `Volatile.Read(ref _state)`, `Unsafe.As<T>(ref _slot)`. Property accessors can't expose `ref` to their synthesized storage, so atomic counters and lock-free state machines need a real field. Document this with a one-line `<remarks>` or comment when you do it.
  - **Constructor assignment that must bypass setter logic** (validation, change notification, parent wiring).
  - **Storage referenced from a method outside the property** (rare; usually a sign the property is the wrong shape).

  Default to `field`; reach for an explicit backing field only when one of the above applies.

### Local syntax

- **`var` for locals.** Always — including primitive types and `new(...)` results. Lets the reader scan the right-hand side once. The exception is fields/parameters where the explicit type is part of the contract.
- **Targeted `new()` (`Foo bar = new();` / `new(args)`)** when the variable is *typed* (a field, parameter, or `T x = new(...)`). Both `var x = new Foo()` (left-side type from `var`, ctor on the right) and `Foo x = new(...)` (typed left, target-typed `new()` on the right) read cleanly; **never** the redundant `Foo x = new Foo()`. Pick whichever side the type lives on and don't repeat it.

### Constants & maintainability

- **Hoist magic strings/numbers to `private const`** with one-line XML docs explaining the *why*. Especially URL prefixes, separators, capacity hints, and length-of-suffix-style values.
- **No magic numbers in `string.Create` size calculations** — name them (e.g. `ParentDirectorySegmentLength = 3`).

### Pooling & buffer reuse

- **`ArrayPool<T>.Shared.Rent` / `.Return`** for transient byte/char buffers in I/O paths (see `PageWriter` for the pattern). Always pair `Rent` with a `try`/`finally` `Return`.
- **Custom pools for hot allocations.** `PageBuilderPool` + `PageBuilderRental` is the project pattern: a thread-static / concurrent stack of pre-sized `StringBuilder`s, returned via a `readonly struct` rental that calls `Return` on `Dispose`. Use this when a type is allocated thousands of times per emit run.
- **`stream.WriteAsync(buffer.AsMemory(0, length))`** to write a partially-filled rented buffer without copying.

### Span / search APIs

- **`SearchValues<T>` for repeated multi-character searches.** Cache as `private static readonly SearchValues<char>` (e.g. `XmlAttributeParser.WhitespaceChars`) and pass to `IndexOfAny` / `IndexOfAnyExcept`. Faster than `IndexOfAny([...])` for any call site hit more than once.
- **`IndexOfAnyExcept`** for "skip whitespace" / "skip terminator" loops — single intrinsic-backed call instead of a hand-rolled loop.
- **`in` modifier on span/struct parameters** (`in ReadOnlySpan<char>`, `in MarkupResult`) when the parameter is read-only and the struct is large enough that a copy matters.
- **Spans as fields on `ref struct`s** (e.g. `DocXmlScanner`) for stack-only stateful parsers. The struct can hold a `ReadOnlySpan<char>` cursor without ever allocating.
- **`TryFormat` / `TryParse` over `ToString` / `Parse`** when writing into a span buffer — skips the intermediate string allocation.

### Read-mostly lookups

- **`FrozenDictionary<TKey, TValue>` / `FrozenSet<T>`** for tables built once at startup and read many times (see `CatalogIndexes`). Build with `ToFrozenDictionary(StringComparer.Ordinal)`. Lookup is faster than `Dictionary<,>` and the table is immutable.
- **Always pass `StringComparer.Ordinal`** to dictionaries/sets keyed on identifiers, file paths, UIDs. Default culture-aware comparison is wrong for these and 5-10× slower. Same for `StringComparison.Ordinal` on `string.Equals` / `IndexOf`.

### Async & concurrency

- **`ConfigureAwait(false)` on every library `await`.** No exceptions in `src/SourceDocParser*/`. Tests don't need it.
- **`ValueTask` / `ValueTask.CompletedTask`** for hot async paths that may complete synchronously. Avoids `Task` allocation per call. Watch the consumption rules — never `await` a `ValueTask` twice.
- **`IAsyncEnumerable<T>`** for streaming sources (`IAssemblySource.DiscoverAsync`). Consumer pulls with `await foreach` (one of the few legitimate `foreach` uses — there is no indexed alternative).
- **`Parallel.ForEachAsync`** with an explicit `MaxDegreeOfParallelism` over hand-rolled `Task.WhenAll` fan-out. The cap is critical: `MetadataExtractor` uses `MaxParallelCompilations` so we don't OOM on large NuGet sets.
- **`Interlocked.Increment` / `Interlocked.Decrement`** for simple counters under contention. Reserve `lock` for genuine multi-field invariants.

### Suppressions

- **Fix the code, don't silence the rule.** Refactor the call site rather than reaching for an attribute. The analyzer set in this repo (StyleCop, Roslynator, SonarAnalyzer, .NET CA, Blazor.Common) catches real perf and correctness issues; suppressing is the last resort.
- **When suppression is genuinely correct, use `[SuppressMessage]` with a `Justification`.** Per-symbol attribute on the smallest enclosing member: `[SuppressMessage("Category", "RuleId", Justification = "Why this case is intentional.")]`. Project-wide `<NoWarn>` is acceptable only for bulk patterns scoped to a project (e.g. `CA1812` across an entire test project) and must carry a comment in the .csproj explaining the scope.

### Collections continued

- **Collection expressions `[..]` first.** When materializing into a final shape (`T[]`, `Span<T>`, `ReadOnlySpan<T>`, `ImmutableArray<T>`), use `[..source]`/`[a, b, ..tail]` and let the compiler pick the optimal layout — including `[]` for empty.
- **`List<T>` only when the final size is unknown.** If you can compute or upper-bound the count up front, allocate `new T[count]` (or a pooled rental) and write by index. When `List<T>` is unavoidable, **always pass a `capacity` to the constructor**: `new List<T>(expectedCount)`. Capacity-less `new List<T>()` doubles its backing array on every overflow and shows up in allocation profiles immediately.

### Type design

- **`sealed` every class** that isn't designed for inheritance. The default in this repo. Helps inlining and avoids accidental override surface.
- **`readonly record struct`** for immutable value-shaped data: small (≤ 4-5 fields) or holding only references (strings, arrays). Equality and hashing come for free, no GC pressure.
- **`sealed record` (class)** when the record participates in inheritance hierarchies (e.g. `ApiObjectType : ApiType`) or holds many fields.
- **Static helpers** for stateless functions; only the public entry-point class is instance-shaped (already documented above).
- **Singleton comparers (`private sealed class XComparer : IComparer<T>` with `public static readonly XComparer Instance`)** instead of allocating a fresh comparer / lambda per `Array.Sort` call.
- **Entry-point classes, not top-level statements.** Every executable project (`NuStreamDocs.Benchmarks`, future tools, future user-facing examples under `src/examples/`) defines a proper `public static class Program` with a `public static [Task<]int[>] Main(string[] args)` method in its own file. No file-scoped top-level statements — keeps stack traces readable, makes `Program` reachable from tests, and avoids the synthesized partial that StyleCop/Sonar flag.
- **`internal static` helpers** for stateless cross-type utilities (`NavBuilder.ToArray`, `MarkdownIo.ReadAsync`, etc.). Group by responsibility, not by feature. Keep the public surface narrow.
- **Most methods static.** A method that doesn't touch `this` should be `static` — fewer hidden allocations, clearer call sites, devirtualization comes free. Reserve instance methods for the **outer layer**: the public façade types that hold genuine per-instance state (`DocBuilder`, plugin instances). Inner-layer parsers, emitters, and helpers are static `Scan` / `Emit` / `Render` style methods. If a class ends up with only static methods, mark the class `static` too.

### When in doubt

The order of preference is: **for-loop over array → for-loop over `List<T>` → `foreach` over indexable → `foreach` over `IEnumerable<T>` (last resort)**. If a hot path can't be expressed with the top option, leave a one-line comment explaining why.

## Commit messages — Conventional Commits 1.0.0

Use the [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/) shape on every commit.

```
<type>(<scope>): <subject>

<body>

<footers>
```

- **Type** (required): `feat` | `fix` | `perf` | `refactor` | `docs` | `test` | `build` | `ci` | `chore` | `revert`.
- **Scope** (optional): the affected assembly or feature, lowercase, **no** `NuStreamDocs.` prefix. Examples: `nav`, `theme.material`, `bibliography`, `icons.materialdesign`, `markdown-extensions`, `cli`, `serve`, `build`. Omit when the change spans many assemblies in roughly equal measure.
- **Subject**: imperative, lowercase, no trailing period, ~70 chars max — what changed and why it matters in one line.
- **Body**: wrap at ~80 chars; explain the *why* (constraints, alternatives, follow-ups); include benchmark numbers for `perf` commits.
- **Breaking changes**: append `!` after the type (`feat(nav)!:`) and add a `BREAKING CHANGE:` footer with the migration note.
- **Issue links**: `Closes #N` / `Refs #N` in footers.
- **No `Co-Authored-By` trailer** in this repo (separate rule, also still applies).

Pick the type from what the commit *changes*, not what triggered it. Reorganizing code that ships no behavior change is `refactor`, even when motivated by a perf review. Adding a benchmark is `test`. A perf win the diff actually delivers is `perf`. `chore` is the catch-all only when nothing else fits.

The full type table + worked examples (including the `perf` shape with benchmark numbers in the body) are in [CONTRIBUTING.md](CONTRIBUTING.md#commit-style).

## Versioning

`GitVersion.MsBuild` (configured in [GitVersion.yml](GitVersion.yml), `ContinuousDeployment` mode on `main`) computes the version from git history on every build. Each commit on `main` produces a clean `MAJOR.MINOR.PATCH` where the patch component is height-driven — no manual tag bumping, no release ceremony. `Microsoft.SourceLink.GitHub` appends the commit sha to `InformationalVersion` so local + dev assemblies are traceable to a specific commit. The `Release` workflow (`workflow_dispatch`) reads the version GitVersion resolved, builds / packs / signs / pushes to NuGet, then creates the GitHub Release (which also creates the `v$VERSION` tag at HEAD as a side-effect — useful for human auditing, not required by GitVersion).

## Acknowledgements

The metadata extraction pipeline is inspired by — and lifts patterns from — [dotnet/docfx](https://github.com/dotnet/docfx) (MIT). See `LICENSE` for the original docfx attribution.
