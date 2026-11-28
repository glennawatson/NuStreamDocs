# Getting Started with NuStreamDocs

## 1. Create a Console Project

NuStreamDocs is a .NET library rather than a global CLI tool. You build your documentation site from a tiny C# console project, giving you full IntelliSense, compile-time type checking, and single-file Native AOT publishing.

```bash
mkdir mydocs
cd mydocs
dotnet new console
```

## 2. Add Packages

Install the core package, one theme (`Material3` or `Material`), and the plugins your site needs:

```bash
dotnet add package NuStreamDocs
dotnet add package NuStreamDocs.Theme.Material3
dotnet add package NuStreamDocs.Nav
dotnet add package NuStreamDocs.Toc
dotnet add package NuStreamDocs.Highlight
dotnet add package NuStreamDocs.Search.Pagefind
dotnet add package NuStreamDocs.MarkdownExtensions
```

## 3. Configure `DocBuilder` in `Program.cs`

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Highlight;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Nav;
using NuStreamDocs.Search.Pagefind;
using NuStreamDocs.Theme.Material3;
using NuStreamDocs.Toc;

var builtPages = await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteName("My Project"u8)
    .WithSiteUrl("https://docs.example.com"u8)
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseNav(opts => opts with { Prune = true })
    .UseToc()
    .UseHighlight()
    .UsePagefindSearch()
    .UseCommonMarkdownExtensions()
    .BuildAsync();

Console.WriteLine($"Built {builtPages} page(s).");
```

## 4. Author Markdown in `docs/`

```text
mydocs/
  Program.cs
  docs/
    index.md
    guide/
      installation.md
      configuration.md
```

Run `dotnet run` to emit the static site into `site/`. During local authoring, add `NuStreamDocs.Serve` and call `.WatchAndServeAsync()` instead of `.BuildAsync()` for Kestrel hosting and WebSocket LiveReload.

## 5. Configuring with UTF-8 Literals (`"..."u8`)

Option mutators accept `"..."u8` UTF-8 byte spans directly so literal text and URLs never allocate intermediate UTF-16 strings:

```csharp
.UseMaterial3Theme(opts => opts
    .WithSiteName("My Project"u8)
    .WithLogo("images/logo.svg"u8)
    .WithSiteUrl("https://docs.example.com"u8)
    .WithRepoUrl("https://github.com/me/my-project"u8)
    .WithEditUri("https://github.com/me/my-project/edit/main/docs/"u8)
    .WithCopyright("Copyright 2026 My Project"u8))
```

## 6. Native AOT Publishing

All production NuStreamDocs assemblies compile with `IsAotCompatible=true` and zero hot-path reflection:

```bash
dotnet publish -c Release -p:PublishAot=true
```

The resulting native executable embeds all theme templates, CSS, JS, and icon catalogues into one self-contained binary that starts in milliseconds in CI and pre-commit hooks.
