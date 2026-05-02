// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Language → <see cref="Lexer"/> registry built once at configuring time.
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
    public static LexerRegistry Default { get; } = Build();

    /// <summary>Builds a registry containing the built-ins plus <paramref name="extra"/> from string-shaped <c>(LanguageId, Lexer)</c> pairs.</summary>
    /// <param name="extra">Additional lexers to register; later entries with the same key win.</param>
    /// <returns>A frozen registry.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="extra"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="extra"/> is empty.</exception>
    /// <remarks>Encodes each language id to UTF-8 once and ASCII-lowercases it so the per-block <see cref="TryGet(System.ReadOnlySpan{byte}, out Lexer?)"/> probe stays byte-only.</remarks>
    public static LexerRegistry CreateFromStringLexers(params (string LanguageId, Lexer Lexer)[] extra)
    {
        ArgumentNullException.ThrowIfNull(extra);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extra.Length);
        var values = new LexerNameValue[extra.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = new(
                AsciiByteHelpers.ToLowerCaseInvariant(Encoding.UTF8.GetBytes(extra[i].LanguageId)),
                extra[i].Lexer);
        }

        return Build(values);
    }

    /// <summary>Builds a registry containing the built-ins plus <paramref name="extra"/>.</summary>
    /// <param name="extra">Additional lexers to register; later entries with the same key win.</param>
    /// <returns>A frozen registry.</returns>
    public static LexerRegistry Build(params LexerNameValue[] extra)
    {
        ArgumentNullException.ThrowIfNull(extra);
        var map = BuildBuiltInAliasMap();
        ApplyExtras(map, extra);
        var (aliases, lexers) = BucketByLength(map, nameof(extra));
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
            if (!AsciiByteHelpers.EqualsIgnoreAsciiCase(language, aliasBucket[i]))
            {
                continue;
            }

            lexer = lexerBucket[i];
            return true;
        }

        return false;
    }

    /// <summary>Returns the byte-keyed map of every built-in language alias to its <see cref="Lexer"/>.</summary>
    /// <returns>The built-in alias map.</returns>
    /// <remarks>Pulled out of <see cref="Build(LexerNameValue[])"/> so the declarative section stays readable and the bucketing step can be reasoned about independently.</remarks>
    [SuppressMessage(
        "Major Code Smell",
        "S138:Methods should not have too many lines",
        Justification = "Single declarative alias→lexer map; one entry per language alias with no branching.")]
    [SuppressMessage(
        "Major Code Smell",
        "S125:Sections of code should not be commented out",
        Justification = "Not commented out code — the alias-map blocks are headed by section comments.")]
    private static Dictionary<byte[], Lexer> BuildBuiltInAliasMap() =>
        new(ByteArrayComparer.Instance)
        {
            // Fully ported lexers.
            ["csharp"u8.ToArray()] = CSharpLexer.Instance,
            ["cs"u8.ToArray()] = CSharpLexer.Instance,
            ["c#"u8.ToArray()] = CSharpLexer.Instance,
            ["html"u8.ToArray()] = HtmlLexer.Instance,
            ["xml"u8.ToArray()] = XmlLexer.Instance,
            ["xhtml"u8.ToArray()] = HtmlLexer.Instance,
            ["svg"u8.ToArray()] = XmlLexer.Instance,
            ["typescript"u8.ToArray()] = TypeScriptLexer.Instance,
            ["ts"u8.ToArray()] = TypeScriptLexer.Instance,
            ["tsx"u8.ToArray()] = TypeScriptLexer.Instance,
            ["javascript"u8.ToArray()] = JavaScriptLexer.Instance,
            ["js"u8.ToArray()] = JavaScriptLexer.Instance,
            ["jsx"u8.ToArray()] = JavaScriptLexer.Instance,
            ["mjs"u8.ToArray()] = JavaScriptLexer.Instance,
            ["cjs"u8.ToArray()] = JavaScriptLexer.Instance,
            ["razor"u8.ToArray()] = RazorLexer.Instance,
            ["cshtml"u8.ToArray()] = RazorLexer.Instance,
            ["bash"u8.ToArray()] = BashLexer.Instance,
            ["sh"u8.ToArray()] = BashLexer.Instance,
            ["shell"u8.ToArray()] = BashLexer.Instance,
            ["zsh"u8.ToArray()] = BashLexer.Instance,
            ["json"u8.ToArray()] = JsonLexer.Instance,
            ["yaml"u8.ToArray()] = YamlLexer.Instance,
            ["yml"u8.ToArray()] = YamlLexer.Instance,
            ["diff"u8.ToArray()] = DiffLexer.Instance,
            ["patch"u8.ToArray()] = DiffLexer.Instance,

            // Placeholder pass-through lexers — registered, so authors
            // get the language-X CSS hook and escaped text today; each
            // can be promoted to a real lexer without touching consumers.
            ["python"u8.ToArray()] = PassThroughLexer.Instance,
            ["py"u8.ToArray()] = PassThroughLexer.Instance,
            ["toml"u8.ToArray()] = PassThroughLexer.Instance,
            ["c"u8.ToArray()] = PassThroughLexer.Instance,
            ["cpp"u8.ToArray()] = PassThroughLexer.Instance,
            ["c++"u8.ToArray()] = PassThroughLexer.Instance,
            ["fsharp"u8.ToArray()] = PassThroughLexer.Instance,
            ["fs"u8.ToArray()] = PassThroughLexer.Instance,
            ["f#"u8.ToArray()] = PassThroughLexer.Instance,
            ["go"u8.ToArray()] = PassThroughLexer.Instance,
            ["golang"u8.ToArray()] = PassThroughLexer.Instance,
            ["rust"u8.ToArray()] = PassThroughLexer.Instance,
            ["rs"u8.ToArray()] = PassThroughLexer.Instance,
            ["java"u8.ToArray()] = PassThroughLexer.Instance,
            ["kotlin"u8.ToArray()] = PassThroughLexer.Instance,
            ["kt"u8.ToArray()] = PassThroughLexer.Instance,
            ["swift"u8.ToArray()] = PassThroughLexer.Instance,
            ["ruby"u8.ToArray()] = PassThroughLexer.Instance,
            ["rb"u8.ToArray()] = PassThroughLexer.Instance,
            ["php"u8.ToArray()] = PassThroughLexer.Instance,
            ["lua"u8.ToArray()] = PassThroughLexer.Instance,
            ["sql"u8.ToArray()] = PassThroughLexer.Instance,
            ["css"u8.ToArray()] = PassThroughLexer.Instance,
            ["scss"u8.ToArray()] = PassThroughLexer.Instance,
            ["less"u8.ToArray()] = PassThroughLexer.Instance,
            ["markdown"u8.ToArray()] = PassThroughLexer.Instance,
            ["md"u8.ToArray()] = PassThroughLexer.Instance,
            ["dockerfile"u8.ToArray()] = PassThroughLexer.Instance,
            ["ini"u8.ToArray()] = PassThroughLexer.Instance,
            ["cfg"u8.ToArray()] = PassThroughLexer.Instance,
            ["conf"u8.ToArray()] = PassThroughLexer.Instance,
            ["makefile"u8.ToArray()] = PassThroughLexer.Instance,
            ["make"u8.ToArray()] = PassThroughLexer.Instance,
            ["powershell"u8.ToArray()] = PassThroughLexer.Instance,
            ["ps1"u8.ToArray()] = PassThroughLexer.Instance,
            ["pwsh"u8.ToArray()] = PassThroughLexer.Instance,
            ["nix"u8.ToArray()] = PassThroughLexer.Instance,
            ["text"u8.ToArray()] = PassThroughLexer.Instance,
            ["plain"u8.ToArray()] = PassThroughLexer.Instance,
            ["txt"u8.ToArray()] = PassThroughLexer.Instance,
        };

    /// <summary>Overlays <paramref name="extra"/> onto <paramref name="map"/> with last-write-wins semantics; each language id is ASCII-lowercased once at insert time.</summary>
    /// <param name="map">Mutable alias map.</param>
    /// <param name="extra">Extras to apply.</param>
    private static void ApplyExtras(Dictionary<byte[], Lexer> map, LexerNameValue[] extra)
    {
        for (var i = 0; i < extra.Length; i++)
        {
            var lexer = extra[i];
            map[AsciiByteHelpers.ToLowerCaseInvariant(lexer.LanguageId)] = lexer.Lexer;
        }
    }

    /// <summary>Buckets <paramref name="map"/> by alias byte-length so per-block lookup folds the ASCII case across a single same-length subarray.</summary>
    /// <param name="map">Alias → lexer source.</param>
    /// <param name="extraParameterName">Name of the public parameter to cite when an alias exceeds the length cap.</param>
    /// <returns>The length-bucketed alias and lexer tables.</returns>
    /// <remarks>Two foreach passes over the map's struct enumerator avoid the intermediate <c>KeyValuePair&lt;&gt;[]</c> snapshot that single materialization would cost.</remarks>
    private static (byte[][][] Aliases, Lexer[][] Lexers) BucketByLength(
        Dictionary<byte[], Lexer> map,
        string extraParameterName)
    {
        const int MaxAliasLength = 64;
        var counts = new int[MaxAliasLength];
        var maxLen = 0;
        foreach (var kvp in map)
        {
            var len = kvp.Key.Length;
            if (len >= MaxAliasLength)
            {
                throw new ArgumentOutOfRangeException(
                    extraParameterName,
                    $"Lexer alias '{Encoding.UTF8.GetString(kvp.Key)}' exceeds the {MaxAliasLength}-byte cap.");
            }

            counts[len]++;
            if (len > maxLen)
            {
                maxLen = len;
            }
        }

        var aliases = new byte[maxLen + 1][][];
        var lexers = new Lexer[maxLen + 1][];
        for (var len = 0; len <= maxLen; len++)
        {
            aliases[len] = counts[len] is 0 ? [] : new byte[counts[len]][];
            lexers[len] = counts[len] is 0 ? [] : new Lexer[counts[len]];
        }

        var cursors = new int[maxLen + 1];
        foreach (var kvp in map)
        {
            var key = kvp.Key;
            var len = key.Length;
            var slot = cursors[len]++;
            aliases[len][slot] = key;
            lexers[len][slot] = kvp.Value;
        }

        return (aliases, lexers);
    }
}
