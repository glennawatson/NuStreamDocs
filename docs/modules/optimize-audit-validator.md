# Optimization, HTML Audit & Link Validation

**Packages**: `NuStreamDocs.Optimize`, `NuStreamDocs.Audit`, `NuStreamDocs.LinkValidator`, `NuStreamDocs.Transitions`, `NuStreamDocs.Serve`

## `NuStreamDocs.Optimize`

Minifies emitted HTML (`.UseHtmlMinify()`) and pre-compresses static assets into `.gz` (GZip) and `.br` (Brotli) sibling files (`.UseOptimize()`) so Nginx, Caddy, and CDNs serve pre-compressed responses without runtime CPU cost.

```csharp
using NuStreamDocs.Optimize;

builder
    .UseHtmlMinify()
    .UseOptimize();
```

## `NuStreamDocs.Audit`

Build-time accessibility and performance linter over the rendered HTML output:

- Missing `<html lang>`, `<title>`, or `<meta name="viewport">`
- Zero or multiple `<h1>` elements, or skipped heading levels (`h2` -> `h4`)
- `<img>` missing `alt` or missing `width` / `height`
- Empty `<a>` / `<button>` elements, positive `tabindex`, unlabeled form inputs, and render-blocking `<script src>` in `<head>`

```csharp
using NuStreamDocs.Audit;

builder.UseAudit(opts => opts.WithStrict(true));
```

## `NuStreamDocs.LinkValidator`

Validates internal relative links, `#fragment` heading anchors, and nav-to-disk consistency (`Strict = true` fails CI on broken links like `mkdocs build --strict`), with optional Polly-throttled HTTP `HEAD` checks for external URLs.

```csharp
using NuStreamDocs.LinkValidator;

builder.UseLinkValidator(opts => opts with { Strict = true });
```
