# Math, Emoji, Keys, SmartSymbols & MagicLink

**Packages**: `NuStreamDocs.Arithmatex`, `NuStreamDocs.Arithmatex.MathJax`, `NuStreamDocs.Emoji`, `NuStreamDocs.Keys`, `NuStreamDocs.SmartSymbols`, `NuStreamDocs.MagicLink`

## `NuStreamDocs.Arithmatex` & `NuStreamDocs.Arithmatex.MathJax`

Wraps inline `$E = mc^2$` and display `$$\int_0^\infty e^{-x}\,dx$$` TeX expressions in `<span class="arithmatex">` / `<div class="arithmatex">` containers, and injects the client-side MathJax 3 runtime when paired with `.UseMathJax()`.

```csharp
using NuStreamDocs.Arithmatex;
using NuStreamDocs.Arithmatex.MathJax;

builder
    .UseArithmatex()
    .UseMathJax();
```

## `NuStreamDocs.Emoji`

Expands `:smile:`, `:rocket:`, and `:warning:` shortcodes into Twemoji spans using a compiled UTF-8 lookup table:

```csharp
using NuStreamDocs.Emoji;

builder.UseEmoji();
```

## `NuStreamDocs.Keys`

Renders keyboard shortcut chord notation (`++ctrl+alt+del++`, `++cmd+shift+p++`) into structured `<span class="keys"><kbd>...</kbd></span>` markup:

```csharp
using NuStreamDocs.Keys;

builder.UseKeys();
```

## `NuStreamDocs.SmartSymbols`

Converts ASCII symbol sequences (`(c)`, `(r)`, `(tm)`, `c/o`, `+/-`, `!=`, `->`, `<-`, `<->`, `1/2`, `1/4`, `3/4`) into typographic Unicode characters while leaving code blocks and inline code untouched:

```csharp
using NuStreamDocs.SmartSymbols;

builder.UseSmartSymbols();
```

## `NuStreamDocs.MagicLink`

Autolinks bare `https://...`, `http://...`, `www....`, and email addresses in prose paragraphs without requiring `<...>` brackets:

```csharp
using NuStreamDocs.MagicLink;

builder.UseMagicLink();
```
