# Snippets, Macros & Layouts

**Packages**: `NuStreamDocs.Snippets`, `NuStreamDocs.Macros`, `NuStreamDocs.Layouts`

## `NuStreamDocs.Snippets`

Splices whole files (`--8<-- "path"`) or named sections (`--8<-- "path#section"`) into Markdown pages during pre-render.

### Setup & Authoring

```csharp
using NuStreamDocs.Snippets;

builder.UseSnippets();
```

Define reusable sections with standard HTML comments inside a partial file:

```markdown
<!-- @section install -->
Run `dotnet add package NuStreamDocs` to get started.
<!-- @endsection -->
```

Include either the entire file or a single named section:

```markdown
--8<-- "_partials/common.md#install"
```

---

## `NuStreamDocs.Macros`

Replaces `{{ variable }}` placeholders in Markdown using a caller-provided `Dictionary<string, string>`, automatically skipping fenced code blocks and inline code spans.

```csharp
using NuStreamDocs.Macros;

builder.UseMacros(opts => opts with
{
    Variables = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["product"] = "NuStreamDocs",
        ["min_sdk"] = ".NET 10.0",
    },
});
```

---

## `NuStreamDocs.Layouts`

Provides Jinja-style `{% extends "base.md" %}` and `{% block content %}...{% endblock %}` template inheritance across Markdown documents so multi-page guides can share consistent headers, disclaimers, or boilerplate sections.

```csharp
using NuStreamDocs.Layouts;

builder.UseLayouts();
```
