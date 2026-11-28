# Responsive Images, Dimensions & Figures (`NuStreamDocs.Images`)

`NuStreamDocs.Images` optimizes rendered `<img>` elements at build time in pure managed C# with zero native image libraries:

- **Zero-CLS Intrinsic Dimensions**: Reads binary and UTF-8 headers directly from local **PNG**, **JPEG**, **GIF**, **WebP** (`VP8`, `VP8L`, `VP8X`), **AVIF/HEIF** (`ispe`), and **SVG** (`width`/`height` or `viewBox`) files and injects missing `width` and `height` attributes. This eliminates Cumulative Layout Shift (CLS) and satisfies `NuStreamDocs.Audit` (`ImageMissingDimensions`) automatically.
- **Lazy & Above-the-Fold Loading**: Adds `loading="lazy"` by default, with `.WithEagerFirstImages(N)` to emit `loading="eager"` on hero images at the top of a page.
- **Async Decoding**: Adds `decoding="async"` so image rasterization stays off the main browser thread.
- **Accessible `<figure>` Captions**: Wraps `<img ... title="Caption">` elements in `<figure class="nd-figure">...<figcaption class="nd-figcaption">Caption</figcaption></figure>` when not already inside a `<figure>`, and ships `/assets/stylesheets/images.css`.

Each local image file is probed at most once per build (reading at most the first 16 KB of header bytes) and cached in memory.

## Installation

```bash
dotnet add package NuStreamDocs.Images
```

## Quick Start

```csharp
using NuStreamDocs.Audit;
using NuStreamDocs.Building;
using NuStreamDocs.Images;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterial3Theme()
    .UseImages(opts => opts.WithEagerFirstImages(1))
    .UseAudit()
    .BuildAsync();
```

## Markdown Input & Rendered Output

Given `docs/architecture.png` (`800x450` PNG) and Markdown:

```markdown
![Architecture overview](architecture.png "Request pipeline stages")
```

`NuStreamDocs.Images` rewrites the rendered HTML to:

```html
<figure class="nd-figure"><img src="architecture.png" alt="Architecture overview" title="Request pipeline stages" width="800" height="450" loading="lazy" decoding="async"><figcaption class="nd-figcaption">Request pipeline stages</figcaption></figure>
```

## Supported Image Formats (`ImageHeaderReader`)

You can also call `ImageHeaderReader.TryReadDimensions(ReadOnlySpan<byte> data, out ImageDimensions dimensions)` directly from C#:

| Format | Header / Chunk Read |
|---|---|
| **PNG** | 8-byte signature + `IHDR` big-endian 32-bit width & height |
| **JPEG** | `SOI` (`FF D8`) + marker walk to `SOF0`..`SOF3` (`FF C0`..`FF C3`) |
| **GIF** | `GIF87a` / `GIF89a` logical screen descriptor |
| **WebP** | `RIFF....WEBP` with `VP8 ` (lossy), `VP8L` (lossless), and `VP8X` (extended 24-bit) |
| **AVIF / HEIF** | ISO BMFF `ftyp` container + `ispe` spatial extents box |
| **SVG** | Root `<svg>` numeric `width`/`height` attributes (`px` or unitless) or `viewBox` fallback |

## Options Reference (`ImagesOptions`)

| Property / Fluent Helper | Default | Purpose |
|---|---|---|
| `Enabled` / `.Disable()` | `true` | Master toggle for HTML rewriting and stylesheet emission. |
| `InjectDimensions` / `.WithoutDimensions()` | `true` | Injects missing `width` and `height` from local image headers. |
| `LazyLoading` / `.WithoutLazyLoading()` | `true` | Adds `loading="lazy"` after the first `EagerFirstCount` images. |
| `EagerFirstCount` / `.WithEagerFirstImages(count)` | `0` | Emits `loading="eager"` on the first `count` images per page. |
| `AsyncDecoding` / `.WithoutAsyncDecoding()` | `true` | Adds `decoding="async"` when `decoding` is absent. |
| `WrapTitledInFigure` / `.WithoutFigureWrapping()` | `true` | Wraps titled images outside `<figure>` in `<figure>` + `<figcaption>`. |
