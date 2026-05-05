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
            [[.. "csharp"u8]] = CSharpLexer.Instance,
            [[.. "cs"u8]] = CSharpLexer.Instance,
            [[.. "c#"u8]] = CSharpLexer.Instance,
            [[.. "html"u8]] = HtmlLexer.Instance,
            [[.. "xml"u8]] = XmlLexer.Instance,
            [[.. "xhtml"u8]] = HtmlLexer.Instance,
            [[.. "svg"u8]] = XmlLexer.Instance,
            [[.. "typescript"u8]] = TypeScriptLexer.Instance,
            [[.. "ts"u8]] = TypeScriptLexer.Instance,
            [[.. "tsx"u8]] = TypeScriptLexer.Instance,
            [[.. "javascript"u8]] = JavaScriptLexer.Instance,
            [[.. "js"u8]] = JavaScriptLexer.Instance,
            [[.. "jsx"u8]] = JavaScriptLexer.Instance,
            [[.. "mjs"u8]] = JavaScriptLexer.Instance,
            [[.. "cjs"u8]] = JavaScriptLexer.Instance,
            [[.. "razor"u8]] = RazorLexer.Instance,
            [[.. "cshtml"u8]] = RazorLexer.Instance,
            [[.. "bash"u8]] = BashLexer.Instance,
            [[.. "sh"u8]] = BashLexer.Instance,
            [[.. "shell"u8]] = BashLexer.Instance,
            [[.. "zsh"u8]] = BashLexer.Instance,
            [[.. "json"u8]] = JsonLexer.Instance,
            [[.. "yaml"u8]] = YamlLexer.Instance,
            [[.. "yml"u8]] = YamlLexer.Instance,
            [[.. "diff"u8]] = DiffLexer.Instance,
            [[.. "patch"u8]] = DiffLexer.Instance,

            [[.. "python"u8]] = PythonLexer.Instance,
            [[.. "py"u8]] = PythonLexer.Instance,

            // Placeholder pass-through lexers — registered, so authors
            // get the language-X CSS hook and escaped text today; each
            // can be promoted to a real lexer without touching consumers.
            [[.. "toml"u8]] = TomlLexer.Instance,
            [[.. "c"u8]] = CLexer.Instance,
            [[.. "h"u8]] = CLexer.Instance,
            [[.. "cpp"u8]] = CppLexer.Instance,
            [[.. "c++"u8]] = CppLexer.Instance,
            [[.. "cxx"u8]] = CppLexer.Instance,
            [[.. "cc"u8]] = CppLexer.Instance,
            [[.. "hpp"u8]] = CppLexer.Instance,
            [[.. "hxx"u8]] = CppLexer.Instance,
            [[.. "fsharp"u8]] = FSharpLexer.Instance,
            [[.. "fs"u8]] = FSharpLexer.Instance,
            [[.. "f#"u8]] = FSharpLexer.Instance,
            [[.. "go"u8]] = GoLexer.Instance,
            [[.. "golang"u8]] = GoLexer.Instance,
            [[.. "rust"u8]] = RustLexer.Instance,
            [[.. "rs"u8]] = RustLexer.Instance,
            [[.. "java"u8]] = JavaLexer.Instance,
            [[.. "kotlin"u8]] = KotlinLexer.Instance,
            [[.. "kt"u8]] = KotlinLexer.Instance,
            [[.. "kts"u8]] = KotlinLexer.Instance,
            [[.. "swift"u8]] = SwiftLexer.Instance,
            [[.. "ruby"u8]] = RubyLexer.Instance,
            [[.. "rb"u8]] = RubyLexer.Instance,
            [[.. "php"u8]] = PhpLexer.Instance,
            [[.. "phtml"u8]] = PhpLexer.Instance,
            [[.. "lua"u8]] = LuaLexer.Instance,
            [[.. "vbnet"u8]] = VbNetLexer.Instance,
            [[.. "vb"u8]] = VbNetLexer.Instance,
            [[.. "vb.net"u8]] = VbNetLexer.Instance,
            [[.. "graphql"u8]] = GraphQLLexer.Instance,
            [[.. "gql"u8]] = GraphQLLexer.Instance,
            [[.. "protobuf"u8]] = ProtobufLexer.Instance,
            [[.. "proto"u8]] = ProtobufLexer.Instance,
            [[.. "hcl"u8]] = HclLexer.Instance,
            [[.. "terraform"u8]] = HclLexer.Instance,
            [[.. "tf"u8]] = HclLexer.Instance,
            [[.. "r"u8]] = RLexer.Instance,
            [[.. "rscript"u8]] = RLexer.Instance,
            [[.. "splus"u8]] = RLexer.Instance,
            [[.. "julia"u8]] = JuliaLexer.Instance,
            [[.. "jl"u8]] = JuliaLexer.Instance,
            [[.. "matlab"u8]] = MatlabLexer.Instance,
            [[.. "octave"u8]] = MatlabLexer.Instance,
            [[.. "nim"u8]] = NimLexer.Instance,
            [[.. "nimrod"u8]] = NimLexer.Instance,
            [[.. "crystal"u8]] = CrystalLexer.Instance,
            [[.. "cr"u8]] = CrystalLexer.Instance,
            [[.. "v"u8]] = VLexer.Instance,
            [[.. "vlang"u8]] = VLexer.Instance,
            [[.. "erlang"u8]] = ErlangLexer.Instance,
            [[.. "erl"u8]] = ErlangLexer.Instance,
            [[.. "elixir"u8]] = ElixirLexer.Instance,
            [[.. "ex"u8]] = ElixirLexer.Instance,
            [[.. "exs"u8]] = ElixirLexer.Instance,
            [[.. "scala"u8]] = ScalaLexer.Instance,
            [[.. "sc"u8]] = ScalaLexer.Instance,
            [[.. "groovy"u8]] = GroovyLexer.Instance,
            [[.. "gradle"u8]] = GroovyLexer.Instance,
            [[.. "dart"u8]] = DartLexer.Instance,
            [[.. "objc"u8]] = ObjectiveCLexer.Instance,
            [[.. "objective-c"u8]] = ObjectiveCLexer.Instance,
            [[.. "objectivec"u8]] = ObjectiveCLexer.Instance,
            [[.. "zig"u8]] = ZigLexer.Instance,
            [[.. "elm"u8]] = ElmLexer.Instance,
            [[.. "commonlisp"u8]] = CommonLispLexer.Instance,
            [[.. "common-lisp"u8]] = CommonLispLexer.Instance,
            [[.. "cl"u8]] = CommonLispLexer.Instance,
            [[.. "ocaml"u8]] = OcamlLexer.Instance,
            [[.. "ml"u8]] = OcamlLexer.Instance,
            [[.. "haskell"u8]] = HaskellLexer.Instance,
            [[.. "hs"u8]] = HaskellLexer.Instance,
            [[.. "clojure"u8]] = ClojureLexer.Instance,
            [[.. "clj"u8]] = ClojureLexer.Instance,
            [[.. "cljs"u8]] = ClojureLexer.Instance,
            [[.. "cljc"u8]] = ClojureLexer.Instance,
            [[.. "edn"u8]] = ClojureLexer.Instance,
            [[.. "scheme"u8]] = SchemeLexer.Instance,
            [[.. "scm"u8]] = SchemeLexer.Instance,
            [[.. "racket"u8]] = SchemeLexer.Instance,
            [[.. "rkt"u8]] = SchemeLexer.Instance,
            [[.. "lisp"u8]] = SchemeLexer.Instance,
            [[.. "elisp"u8]] = SchemeLexer.Instance,
            [[.. "el"u8]] = SchemeLexer.Instance,
            [[.. "ksh"u8]] = BashLexer.Instance,
            [[.. "fish"u8]] = BashLexer.Instance,
            [[.. "tcsh"u8]] = BashLexer.Instance,
            [[.. "csh"u8]] = BashLexer.Instance,
            [[.. "sql"u8]] = SqlLexer.Instance,
            [[.. "psql"u8]] = SqlLexer.Instance,
            [[.. "mysql"u8]] = SqlLexer.Instance,
            [[.. "tsql"u8]] = SqlLexer.Instance,
            [[.. "css"u8]] = CssLexer.Instance,
            [[.. "scss"u8]] = ScssLexer.Instance,
            [[.. "sass"u8]] = ScssLexer.Instance,
            [[.. "less"u8]] = LessLexer.Instance,
            [[.. "markdown"u8]] = MarkdownLexer.Instance,
            [[.. "md"u8]] = MarkdownLexer.Instance,
            [[.. "mdx"u8]] = MarkdownLexer.Instance,
            [[.. "dockerfile"u8]] = DockerfileLexer.Instance,
            [[.. "docker"u8]] = DockerfileLexer.Instance,
            [[.. "ini"u8]] = IniLexer.Instance,
            [[.. "cfg"u8]] = IniLexer.Instance,
            [[.. "conf"u8]] = IniLexer.Instance,
            [[.. "editorconfig"u8]] = IniLexer.Instance,
            [[.. "gitconfig"u8]] = IniLexer.Instance,
            [[.. "systemd"u8]] = IniLexer.Instance,
            [[.. "properties"u8]] = PropertiesLexer.Instance,
            [[.. "makefile"u8]] = MakefileLexer.Instance,
            [[.. "make"u8]] = MakefileLexer.Instance,
            [[.. "mf"u8]] = MakefileLexer.Instance,
            [[.. "bsdmake"u8]] = MakefileLexer.Instance,
            [[.. "cmake"u8]] = CMakeLexer.Instance,
            [[.. "powershell"u8]] = PowerShellLexer.Instance,
            [[.. "ps1"u8]] = PowerShellLexer.Instance,
            [[.. "psm1"u8]] = PowerShellLexer.Instance,
            [[.. "pwsh"u8]] = PowerShellLexer.Instance,
            [[.. "posh"u8]] = PowerShellLexer.Instance,
            [[.. "nix"u8]] = NixLexer.Instance,
            [[.. "jinja"u8]] = JinjaLexer.Instance,
            [[.. "jinja2"u8]] = JinjaLexer.Instance,
            [[.. "twig"u8]] = JinjaLexer.Instance,
            [[.. "django"u8]] = JinjaLexer.Instance,
            [[.. "liquid"u8]] = LiquidLexer.Instance,
            [[.. "erb"u8]] = ErbLexer.Instance,
            [[.. "ejs"u8]] = ErbLexer.Instance,
            [[.. "handlebars"u8]] = HandlebarsLexer.Instance,
            [[.. "hbs"u8]] = HandlebarsLexer.Instance,
            [[.. "mustache"u8]] = HandlebarsLexer.Instance,
            [[.. "asm"u8]] = X86AsmLexer.Instance,
            [[.. "nasm"u8]] = X86AsmLexer.Instance,
            [[.. "gas"u8]] = X86AsmLexer.Instance,
            [[.. "x86"u8]] = X86AsmLexer.Instance,
            [[.. "x86asm"u8]] = X86AsmLexer.Instance,
            [[.. "arm"u8]] = ArmAsmLexer.Instance,
            [[.. "armasm"u8]] = ArmAsmLexer.Instance,
            [[.. "aarch64"u8]] = ArmAsmLexer.Instance,
            [[.. "wat"u8]] = WatLexer.Instance,
            [[.. "wast"u8]] = WatLexer.Instance,
            [[.. "wasm"u8]] = WatLexer.Instance,
            [[.. "perl"u8]] = PerlLexer.Instance,
            [[.. "pl"u8]] = PerlLexer.Instance,
            [[.. "pm"u8]] = PerlLexer.Instance,
            [[.. "perl5"u8]] = PerlLexer.Instance,
            [[.. "text"u8]] = PassThroughLexer.Instance,
            [[.. "plain"u8]] = PassThroughLexer.Instance,
            [[.. "txt"u8]] = PassThroughLexer.Instance
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
        foreach (var (key, value) in map)
        {
            var len = key.Length;
            var slot = cursors[len]++;
            aliases[len][slot] = key;
            lexers[len][slot] = value;
        }

        return (aliases, lexers);
    }
}
