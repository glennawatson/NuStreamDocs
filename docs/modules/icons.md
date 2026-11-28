# Icon Packs (`MaterialDesign`, `Material`, `FontAwesome`)

**Packages**: `NuStreamDocs.Icons.MaterialDesign`, `NuStreamDocs.Icons.Material`, `NuStreamDocs.Icons.FontAwesome`

## `NuStreamDocs.Icons.MaterialDesign` (Inline SVG)

Bakes the ~7,400-icon Pictogrammers Material Design Icons (MDI) SVG path catalogue directly into compiled UTF-8 byte tables (~50 ns lookup, zero startup cost). Pass `new MdiIconResolver()` to either theme to inline `:material-*:` shortcodes directly as `<svg>` elements:

```csharp
using NuStreamDocs.Icons.MaterialDesign;
using NuStreamDocs.Theme.Material3;

builder.UseMaterial3Theme(new MdiIconResolver());
```

In Markdown:

```markdown
:material-rocket-launch: Launch the service
```

## `NuStreamDocs.Icons.Material` (Webfont Ligatures)

Injects the Google Material Icons / Material Symbols (Outlined, Rounded, or Sharp) stylesheet `<link>` so font-ligature icon spans render:

```csharp
using NuStreamDocs.Icons.Material;

builder.UseMaterialIcons();
```

## `NuStreamDocs.Icons.FontAwesome`

Injects Font Awesome Free stylesheet links from a configurable CDN URL:

```csharp
using NuStreamDocs.Icons.FontAwesome;

builder.UseFontAwesome();
```
