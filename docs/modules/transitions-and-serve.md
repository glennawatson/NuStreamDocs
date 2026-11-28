# View Transitions & Live-Reload Dev Server

**Packages**: `NuStreamDocs.Transitions`, `NuStreamDocs.Serve`

## `NuStreamDocs.Transitions`

Ships a lightweight, framework-free client router that intercepts same-origin page clicks, prefetches links on hover (`PrefetchDelayMs = 80`), swaps the main content, table of contents, primary sidebar, top tabs, header title/edit link, language switcher, and footer in place using the browser's View Transitions API (`TransitionAnimation.Fade`), and preserves search state and sidebar scroll position. Degrades cleanly to standard browser navigation when JavaScript, View Transitions, or per-page CSP hashes require a full reload.

```csharp
using NuStreamDocs.Transitions;

builder.UseTransitions(opts => opts
    .WithAnimation(TransitionAnimation.Fade)
    .WithPrefetch(PrefetchStrategy.Hover)
    .WithPrefetchDelay(80));
```

### Lifecycle Events

Custom scripts can hook into soft navigation via document-level `CustomEvent`s:

- `nstd:before-swap` (`detail.to`, `detail.newDocument`): Fired right before DOM regions are updated.
- `nstd:page-load` (`detail.url`): Fired on initial load and after each soft navigation completes (`Mermaid`, `Lightbox`, `MathJax`, and TOC scroll-spy re-bind automatically).

---

## `NuStreamDocs.Serve`

Replaces `BuildAsync()` during local authoring with `WatchAndServeAsync()`. Runs an initial build, hosts the output directory on Kestrel (`http://127.0.0.1:8000` by default), watches `InputRoot` with a `250 ms` debounce filter (ignoring `bin`, `obj`, `.git`, and `OutputRoot`), and triggers instant browser refreshes over a WebSocket LiveReload channel.

```csharp
using NuStreamDocs.Serve;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterial3Theme()
    .UseNav()
    .UseToc()
    .UseHighlight()
    .WatchAndServeAsync(opts => opts with
    {
        Port = 8000,
        OpenBrowser = true,
        LiveReload = true,
    });
```
