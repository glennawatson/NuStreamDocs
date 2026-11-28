# View Transitions & Live-Reload Dev Server

**Packages**: `NuStreamDocs.Transitions`, `NuStreamDocs.Serve`

## `NuStreamDocs.Transitions`

Ships a lightweight, framework-free client router that intercepts same-origin page clicks, prefetches links on hover (`PrefetchDelayMs = 80`), swaps the main content and TOC regions in place using the browser's View Transitions API (`TransitionAnimation.Fade` / `Slide`), and preserves sidebar scroll state. Degrades cleanly to standard browser navigation when JavaScript or View Transitions are unavailable.

```csharp
using NuStreamDocs.Transitions;

builder.UseTransitions(opts => opts
    .WithAnimation(TransitionAnimation.Fade)
    .WithPrefetch(PrefetchStrategy.Hover)
    .WithPrefetchDelayMs(80));
```

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
