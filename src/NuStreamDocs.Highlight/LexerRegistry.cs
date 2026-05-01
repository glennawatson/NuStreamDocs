// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Frozen language → <see cref="Lexer"/> map built once at configure
/// time so per-block lookup is O(1) on the render hot path.
/// </summary>
/// <remarks>
/// Built-in lexers register here automatically. Consumers add custom
/// lexers via <see cref="HighlightOptions"/> and the resulting registry
/// is what the plugin keeps for the lifetime of the build.
/// </remarks>
public sealed class LexerRegistry
{
    /// <summary>The frozen storage.</summary>
    private readonly FrozenDictionary<string, Lexer> _lexers;

    /// <summary>Initializes a new instance of the <see cref="LexerRegistry"/> class.</summary>
    /// <param name="lexers">Frozen language-name → lexer map.</param>
    private LexerRegistry(FrozenDictionary<string, Lexer> lexers) => _lexers = lexers;

    /// <summary>Gets the default registry — every built-in language.</summary>
    public static LexerRegistry Default { get; } = Build([]);

    /// <summary>Builds a registry containing the built-ins plus <paramref name="extra"/>.</summary>
    /// <param name="extra">Additional lexers to register; later entries with the same key win.</param>
    /// <returns>A frozen registry.</returns>
    public static LexerRegistry Build(Lexer[] extra)
    {
        ArgumentNullException.ThrowIfNull(extra);

        var map = new Dictionary<string, Lexer>(StringComparer.OrdinalIgnoreCase)
        {
            // Fully ported lexers.
            ["csharp"] = CSharpLexer.Instance,
            ["cs"] = CSharpLexer.Instance,
            ["c#"] = CSharpLexer.Instance,
            ["html"] = HtmlLexer.Instance,
            ["xml"] = XmlLexer.Instance,
            ["xhtml"] = HtmlLexer.Instance,
            ["svg"] = XmlLexer.Instance,
            ["typescript"] = TypeScriptLexer.Instance,
            ["ts"] = TypeScriptLexer.Instance,
            ["tsx"] = TypeScriptLexer.Instance,
            ["javascript"] = JavaScriptLexer.Instance,
            ["js"] = JavaScriptLexer.Instance,
            ["jsx"] = JavaScriptLexer.Instance,
            ["mjs"] = JavaScriptLexer.Instance,
            ["cjs"] = JavaScriptLexer.Instance,
            ["razor"] = RazorLexer.Instance,
            ["cshtml"] = RazorLexer.Instance,
            ["bash"] = BashLexer.Instance,
            ["sh"] = BashLexer.Instance,
            ["shell"] = BashLexer.Instance,
            ["zsh"] = BashLexer.Instance,
            ["json"] = JsonLexer.Instance,
            ["yaml"] = YamlLexer.Instance,
            ["yml"] = YamlLexer.Instance,
            ["diff"] = DiffLexer.Instance,
            ["patch"] = DiffLexer.Instance,

            // Placeholder pass-through lexers — registered so authors
            // get the language-X CSS hook and escaped text today; each
            // can be promoted to a real lexer without touching consumers.
            ["python"] = PlaceholderLexers.Python,
            ["py"] = PlaceholderLexers.Python,
            ["toml"] = PlaceholderLexers.Toml,
            ["c"] = PlaceholderLexers.C,
            ["cpp"] = PlaceholderLexers.Cpp,
            ["c++"] = PlaceholderLexers.Cpp,
            ["fsharp"] = PlaceholderLexers.FSharp,
            ["fs"] = PlaceholderLexers.FSharp,
            ["f#"] = PlaceholderLexers.FSharp,
            ["go"] = PlaceholderLexers.Go,
            ["golang"] = PlaceholderLexers.Go,
            ["rust"] = PlaceholderLexers.Rust,
            ["rs"] = PlaceholderLexers.Rust,
            ["java"] = PlaceholderLexers.Java,
            ["kotlin"] = PlaceholderLexers.Kotlin,
            ["kt"] = PlaceholderLexers.Kotlin,
            ["swift"] = PlaceholderLexers.Swift,
            ["ruby"] = PlaceholderLexers.Ruby,
            ["rb"] = PlaceholderLexers.Ruby,
            ["php"] = PlaceholderLexers.Php,
            ["lua"] = PlaceholderLexers.Lua,
            ["sql"] = PlaceholderLexers.Sql,
            ["css"] = PlaceholderLexers.Css,
            ["scss"] = PlaceholderLexers.Css,
            ["less"] = PlaceholderLexers.Css,
            ["markdown"] = PlaceholderLexers.Markdown,
            ["md"] = PlaceholderLexers.Markdown,
            ["dockerfile"] = PlaceholderLexers.Dockerfile,
            ["ini"] = PlaceholderLexers.Ini,
            ["cfg"] = PlaceholderLexers.Ini,
            ["conf"] = PlaceholderLexers.Ini,
            ["makefile"] = PlaceholderLexers.Makefile,
            ["make"] = PlaceholderLexers.Makefile,
            ["powershell"] = PlaceholderLexers.PowerShell,
            ["ps1"] = PlaceholderLexers.PowerShell,
            ["pwsh"] = PlaceholderLexers.PowerShell,
            ["nix"] = PlaceholderLexers.Nix,
            ["text"] = PlaceholderLexers.PlainText,
            ["plain"] = PlaceholderLexers.PlainText,
            ["txt"] = PlaceholderLexers.PlainText,
        };

        for (var i = 0; i < extra.Length; i++)
        {
            var lexer = extra[i];
            map[lexer.LanguageName] = lexer;
        }

        return new(map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Tries to resolve <paramref name="language"/> (case-insensitive) to a registered lexer.</summary>
    /// <param name="language">Language name from a fenced-code info string.</param>
    /// <param name="lexer">Resolved lexer on success.</param>
    /// <returns>True when registered.</returns>
    public bool TryGet(string language, out Lexer lexer)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);
        return _lexers.TryGetValue(language, out lexer!);
    }
}
