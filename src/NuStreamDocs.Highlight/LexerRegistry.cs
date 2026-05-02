// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Language → <see cref="Lexer"/> registry built once at configure time.
/// </summary>
/// <remarks>
/// Lookup is byte-keyed and case-insensitive — the alias arrives as a
/// UTF-8 byte slice from the rendered HTML, so no string allocation is
/// needed on the per-block hot path. Internally the registry stores
/// each alias as a pre-lowercased <c>byte[]</c>; lookup folds the
/// candidate's ASCII case via <see cref="AsciiByteHelpers"/>.
/// </remarks>
public sealed class LexerRegistry
{
    /// <summary>Length-bucketed alias table — <c>_aliasesByLength[len][i]</c> is the lowercased alias bytes; <c>_lexersByLength[len][i]</c> is the matching lexer.</summary>
    private readonly byte[][][] _aliasesByLength;

    /// <summary>Length-bucketed lexer table parallel to <see cref="_aliasesByLength"/>.</summary>
    private readonly Lexer[][] _lexersByLength;

    /// <summary>Initializes a new instance of the <see cref="LexerRegistry"/> class.</summary>
    /// <param name="aliasesByLength">Length-bucketed alias table.</param>
    /// <param name="lexersByLength">Length-bucketed lexer table.</param>
    private LexerRegistry(byte[][][] aliasesByLength, Lexer[][] lexersByLength)
    {
        _aliasesByLength = aliasesByLength;
        _lexersByLength = lexersByLength;
    }

    /// <summary>Gets the default registry — every built-in language.</summary>
    public static LexerRegistry Default { get; } = Build([]);

    /// <summary>Builds a registry containing the built-ins plus <paramref name="extra"/>.</summary>
    /// <param name="extra">Additional lexers to register; later entries with the same key win.</param>
    /// <returns>A frozen registry.</returns>
    [SuppressMessage(
        "Major Code Smell",
        "S138:Methods should not have too many lines",
        Justification = "Single declarative alias→lexer map followed by a small bucketing pass; splitting it just relocates the literals without reducing complexity.")]
    [SuppressMessage(
        "Major Code Smell",
        "S125:Sections of code should not be commented out",
        Justification = "Not commented out code — the alias-map blocks are headed by section comments.")]
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

        // Bucket aliases by length for the byte-keyed lookup. Each alias is
        // pre-lowercased so the per-block compare is one ASCII case-fold pass
        // (StartsWithIgnoreAsciiCase contract).
        var maxLen = 0;
        foreach (var key in map.Keys)
        {
            if (key.Length > maxLen)
            {
                maxLen = key.Length;
            }
        }

        var counts = new int[maxLen + 1];
        foreach (var key in map.Keys)
        {
            counts[key.Length]++;
        }

        var aliases = new byte[maxLen + 1][][];
        var lexers = new Lexer[maxLen + 1][];
        for (var len = 0; len <= maxLen; len++)
        {
            aliases[len] = counts[len] is 0 ? [] : new byte[counts[len]][];
            lexers[len] = counts[len] is 0 ? [] : new Lexer[counts[len]];
        }

        var cursors = new int[maxLen + 1];
        foreach (var (key, lexer) in map)
        {
            var len = key.Length;
            var slot = cursors[len]++;
            aliases[len][slot] = Encoding.UTF8.GetBytes(key.ToLowerInvariant());
            lexers[len][slot] = lexer;
        }

        return new(aliases, lexers);
    }

    /// <summary>Tries to resolve <paramref name="language"/> (case-insensitive ASCII) to a registered lexer.</summary>
    /// <param name="language">Language alias (UTF-8 bytes).</param>
    /// <param name="lexer">Resolved lexer on success.</param>
    /// <returns>True when registered.</returns>
    public bool TryGet(ReadOnlySpan<byte> language, out Lexer? lexer)
    {
        lexer = null;
        if ((uint)language.Length >= (uint)_aliasesByLength.Length)
        {
            return false;
        }

        var aliasBucket = _aliasesByLength[language.Length];
        var lexerBucket = _lexersByLength[language.Length];
        for (var i = 0; i < aliasBucket.Length; i++)
        {
            if (AsciiByteHelpers.EqualsIgnoreAsciiCase(language, aliasBucket[i]))
            {
                lexer = lexerBucket[i];
                return true;
            }
        }

        return false;
    }

    /// <summary>String-keyed convenience overload.</summary>
    /// <param name="language">Language alias.</param>
    /// <param name="lexer">Resolved lexer on success.</param>
    /// <returns>True when registered.</returns>
    public bool TryGet(string language, out Lexer? lexer)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);
        Span<byte> stack = stackalloc byte[256];
        var maxBytes = Encoding.UTF8.GetMaxByteCount(language.Length);
        if (maxBytes <= stack.Length)
        {
            var written = Encoding.UTF8.GetBytes(language, stack);
            return TryGet(stack[..written], out lexer);
        }

        var heap = Encoding.UTF8.GetBytes(language);
        return TryGet(heap, out lexer);
    }
}
