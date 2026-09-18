// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Highlight.Languages.Asm;
using NuStreamDocs.Highlight.Languages.Build;
using NuStreamDocs.Highlight.Languages.CFamily;
using NuStreamDocs.Highlight.Languages.Data;
using NuStreamDocs.Highlight.Languages.Functional;
using NuStreamDocs.Highlight.Languages.Markup;
using NuStreamDocs.Highlight.Languages.Misc;
using NuStreamDocs.Highlight.Languages.Schema;
using NuStreamDocs.Highlight.Languages.Scripting;
using NuStreamDocs.Highlight.Languages.Stylesheet;

namespace NuStreamDocs.Highlight;

/// <summary>Language-alias → <see cref="Lexer"/> registry. Lookup is byte-keyed and ASCII-case-insensitive.</summary>
[System.Diagnostics.DebuggerDisplay("LexerRegistry: {_aliasesByLength}")]
public sealed class LexerRegistry
{
    /// <summary>Length-bucketed alias table.</summary>
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
    public static LexerRegistry CreateFromStringLexers(params (string LanguageId, Lexer Lexer)[] extra)
    {
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

    /// <summary>Registers the CFamily language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterCFamilyAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.. "csharp"u8]] = CSharpLexer.Instance;
        map[[.."cs"u8]] = CSharpLexer.Instance;
        map[[.."c#"u8]] = CSharpLexer.Instance;
        map[[.."c"u8]] = CLexer.Instance;
        map[[.."h"u8]] = CLexer.Instance;
        map[[.."cpp"u8]] = CppLexer.Instance;
        map[[.."c++"u8]] = CppLexer.Instance;
        map[[.."cxx"u8]] = CppLexer.Instance;
        map[[.."cc"u8]] = CppLexer.Instance;
        map[[.."hpp"u8]] = CppLexer.Instance;
        map[[.."hxx"u8]] = CppLexer.Instance;
        map[[.."go"u8]] = GoLexer.Instance;
        map[[.."golang"u8]] = GoLexer.Instance;
        map[[.."rust"u8]] = RustLexer.Instance;
        map[[.."rs"u8]] = RustLexer.Instance;
        map[[.."java"u8]] = JavaLexer.Instance;
        map[[.."kotlin"u8]] = KotlinLexer.Instance;
        map[[.."kt"u8]] = KotlinLexer.Instance;
        map[[.."kts"u8]] = KotlinLexer.Instance;
        map[[.."swift"u8]] = SwiftLexer.Instance;
        map[[.."crystal"u8]] = CrystalLexer.Instance;
        map[[.."cr"u8]] = CrystalLexer.Instance;
        map[[.."v"u8]] = VLexer.Instance;
        map[[.."vlang"u8]] = VLexer.Instance;
        map[[.."scala"u8]] = ScalaLexer.Instance;
        map[[.."sc"u8]] = ScalaLexer.Instance;
        map[[.."groovy"u8]] = GroovyLexer.Instance;
        map[[.."gradle"u8]] = GroovyLexer.Instance;
        map[[.."dart"u8]] = DartLexer.Instance;
        map[[.."objc"u8]] = ObjectiveCLexer.Instance;
        map[[.."objective-c"u8]] = ObjectiveCLexer.Instance;
        map[[.."objectivec"u8]] = ObjectiveCLexer.Instance;
        map[[.."zig"u8]] = ZigLexer.Instance;
        map[[.."glsl"u8]] = GlslLexer.Instance;
        map[[.."vert"u8]] = GlslLexer.Instance;
        map[[.."frag"u8]] = GlslLexer.Instance;
        map[[.."geom"u8]] = GlslLexer.Instance;
        map[[.."tesc"u8]] = GlslLexer.Instance;
        map[[.."tese"u8]] = GlslLexer.Instance;
        map[[.."comp"u8]] = GlslLexer.Instance;
        map[[.."hlsl"u8]] = HlslLexer.Instance;
        map[[.."fx"u8]] = HlslLexer.Instance;
        map[[.."fxh"u8]] = HlslLexer.Instance;
    }

    /// <summary>Registers the Markup language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterMarkupAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."html"u8]] = HtmlLexer.Instance;
        map[[.."xml"u8]] = XmlLexer.Instance;
        map[[.."xhtml"u8]] = HtmlLexer.Instance;
        map[[.."svg"u8]] = XmlLexer.Instance;
        map[[.."razor"u8]] = RazorLexer.Instance;
        map[[.."cshtml"u8]] = RazorLexer.Instance;
        map[[.."markdown"u8]] = MarkdownLexer.Instance;
        map[[.."md"u8]] = MarkdownLexer.Instance;
        map[[.."mdx"u8]] = MarkdownLexer.Instance;
        map[[.."jinja"u8]] = JinjaLexer.Instance;
        map[[.."jinja2"u8]] = JinjaLexer.Instance;
        map[[.."twig"u8]] = JinjaLexer.Instance;
        map[[.."django"u8]] = JinjaLexer.Instance;
        map[[.."liquid"u8]] = LiquidLexer.Instance;
        map[[.."erb"u8]] = ErbLexer.Instance;
        map[[.."ejs"u8]] = ErbLexer.Instance;
        map[[.."handlebars"u8]] = HandlebarsLexer.Instance;
        map[[.."hbs"u8]] = HandlebarsLexer.Instance;
        map[[.."mustache"u8]] = HandlebarsLexer.Instance;
        map[[.."rst"u8]] = RstLexer.Instance;
        map[[.."rest"u8]] = RstLexer.Instance;
        map[[.."restructuredtext"u8]] = RstLexer.Instance;
    }

    /// <summary>Registers the Scripting language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterScriptingAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."typescript"u8]] = TypeScriptLexer.Instance;
        map[[.."ts"u8]] = TypeScriptLexer.Instance;
        map[[.."tsx"u8]] = TypeScriptLexer.Instance;
        map[[.."javascript"u8]] = JavaScriptLexer.Instance;
        map[[.."js"u8]] = JavaScriptLexer.Instance;
        map[[.."jsx"u8]] = JavaScriptLexer.Instance;
        map[[.."mjs"u8]] = JavaScriptLexer.Instance;
        map[[.."cjs"u8]] = JavaScriptLexer.Instance;
        map[[.."bash"u8]] = BashLexer.Instance;
        map[[.."sh"u8]] = BashLexer.Instance;
        map[[.."shell"u8]] = BashLexer.Instance;
        map[[.."zsh"u8]] = BashLexer.Instance;
        map[[.."python"u8]] = PythonLexer.Instance;
        map[[.."py"u8]] = PythonLexer.Instance;
        map[[.."ruby"u8]] = RubyLexer.Instance;
        map[[.."rb"u8]] = RubyLexer.Instance;
        map[[.."php"u8]] = PhpLexer.Instance;
        map[[.."phtml"u8]] = PhpLexer.Instance;
        map[[.."lua"u8]] = LuaLexer.Instance;
        map[[.."vbnet"u8]] = VbNetLexer.Instance;
        map[[.."vb"u8]] = VbNetLexer.Instance;
        map[[.."vb.net"u8]] = VbNetLexer.Instance;
        map[[.."r"u8]] = RLexer.Instance;
        map[[.."rscript"u8]] = RLexer.Instance;
        map[[.."splus"u8]] = RLexer.Instance;
        map[[.."julia"u8]] = JuliaLexer.Instance;
        map[[.."jl"u8]] = JuliaLexer.Instance;
        map[[.."matlab"u8]] = MatlabLexer.Instance;
        map[[.."octave"u8]] = MatlabLexer.Instance;
        map[[.."nim"u8]] = NimLexer.Instance;
        map[[.."nimrod"u8]] = NimLexer.Instance;
        map[[.."erlang"u8]] = ErlangLexer.Instance;
        map[[.."erl"u8]] = ErlangLexer.Instance;
        map[[.."elixir"u8]] = ElixirLexer.Instance;
        map[[.."ex"u8]] = ElixirLexer.Instance;
        map[[.."exs"u8]] = ElixirLexer.Instance;
        map[[.."ksh"u8]] = BashLexer.Instance;
        map[[.."fish"u8]] = BashLexer.Instance;
        map[[.."tcsh"u8]] = BashLexer.Instance;
        map[[.."csh"u8]] = BashLexer.Instance;
        map[[.."powershell"u8]] = PowerShellLexer.Instance;
        map[[.."ps1"u8]] = PowerShellLexer.Instance;
        map[[.."psm1"u8]] = PowerShellLexer.Instance;
        map[[.."pwsh"u8]] = PowerShellLexer.Instance;
        map[[.."posh"u8]] = PowerShellLexer.Instance;
        map[[.."perl"u8]] = PerlLexer.Instance;
        map[[.."pl"u8]] = PerlLexer.Instance;
        map[[.."pm"u8]] = PerlLexer.Instance;
        map[[.."perl5"u8]] = PerlLexer.Instance;
    }

    /// <summary>Registers the Data language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterDataAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."json"u8]] = JsonLexer.Instance;
        map[[.."yaml"u8]] = YamlLexer.Instance;
        map[[.."yml"u8]] = YamlLexer.Instance;
        map[[.."csv"u8]] = CsvLexer.Instance;
        map[[.."tsv"u8]] = CsvLexer.Instance;
        map[[.. "toml"u8]] = TomlLexer.Instance;
        map[[.."sql"u8]] = SqlLexer.Instance;
        map[[.."psql"u8]] = SqlLexer.Instance;
        map[[.."mysql"u8]] = SqlLexer.Instance;
        map[[.."tsql"u8]] = SqlLexer.Instance;
        map[[.."ini"u8]] = IniLexer.Instance;
        map[[.."cfg"u8]] = IniLexer.Instance;
        map[[.."conf"u8]] = IniLexer.Instance;
        map[[.."editorconfig"u8]] = IniLexer.Instance;
        map[[.."gitconfig"u8]] = IniLexer.Instance;
        map[[.."systemd"u8]] = IniLexer.Instance;
        map[[.."properties"u8]] = PropertiesLexer.Instance;
    }

    /// <summary>Registers the Misc language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterMiscAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."diff"u8]] = DiffLexer.Instance;
        map[[.."patch"u8]] = DiffLexer.Instance;
        map[[.."http"u8]] = HttpLexer.Instance;
        map[[.."text"u8]] = PassThroughLexer.Instance;
        map[[.."plain"u8]] = PassThroughLexer.Instance;
        map[[.."txt"u8]] = PassThroughLexer.Instance;
    }

    /// <summary>Registers the Functional language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterFunctionalAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."fsharp"u8]] = FSharpLexer.Instance;
        map[[.."fs"u8]] = FSharpLexer.Instance;
        map[[.."f#"u8]] = FSharpLexer.Instance;
        map[[.."elm"u8]] = ElmLexer.Instance;
        map[[.."commonlisp"u8]] = CommonLispLexer.Instance;
        map[[.."common-lisp"u8]] = CommonLispLexer.Instance;
        map[[.."cl"u8]] = CommonLispLexer.Instance;
        map[[.."ocaml"u8]] = OcamlLexer.Instance;
        map[[.."ml"u8]] = OcamlLexer.Instance;
        map[[.."haskell"u8]] = HaskellLexer.Instance;
        map[[.."hs"u8]] = HaskellLexer.Instance;
        map[[.."clojure"u8]] = ClojureLexer.Instance;
        map[[.."clj"u8]] = ClojureLexer.Instance;
        map[[.."cljs"u8]] = ClojureLexer.Instance;
        map[[.."cljc"u8]] = ClojureLexer.Instance;
        map[[.."edn"u8]] = ClojureLexer.Instance;
        map[[.."scheme"u8]] = SchemeLexer.Instance;
        map[[.."scm"u8]] = SchemeLexer.Instance;
        map[[.."racket"u8]] = SchemeLexer.Instance;
        map[[.."rkt"u8]] = SchemeLexer.Instance;
        map[[.."lisp"u8]] = SchemeLexer.Instance;
        map[[.."elisp"u8]] = SchemeLexer.Instance;
        map[[.."el"u8]] = SchemeLexer.Instance;
    }

    /// <summary>Registers the Schema language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterSchemaAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."graphql"u8]] = GraphQLLexer.Instance;
        map[[.."gql"u8]] = GraphQLLexer.Instance;
        map[[.."protobuf"u8]] = ProtobufLexer.Instance;
        map[[.."proto"u8]] = ProtobufLexer.Instance;
        map[[.."hcl"u8]] = HclLexer.Instance;
        map[[.."terraform"u8]] = HclLexer.Instance;
        map[[.."tf"u8]] = HclLexer.Instance;
    }

    /// <summary>Registers the Stylesheet language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterStylesheetAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."css"u8]] = CssLexer.Instance;
        map[[.."scss"u8]] = ScssLexer.Instance;
        map[[.."sass"u8]] = ScssLexer.Instance;
        map[[.."less"u8]] = LessLexer.Instance;
    }

    /// <summary>Registers the Build language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterBuildAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."dockerfile"u8]] = DockerfileLexer.Instance;
        map[[.."docker"u8]] = DockerfileLexer.Instance;
        map[[.."makefile"u8]] = MakefileLexer.Instance;
        map[[.."make"u8]] = MakefileLexer.Instance;
        map[[.."mf"u8]] = MakefileLexer.Instance;
        map[[.."bsdmake"u8]] = MakefileLexer.Instance;
        map[[.."cmake"u8]] = CMakeLexer.Instance;
        map[[.."nix"u8]] = NixLexer.Instance;
    }

    /// <summary>Registers the Asm language aliases.</summary>
    /// <param name = "map">Destination alias map.</param>
    private static void RegisterAsmAliases(Dictionary<byte[], Lexer> map)
    {
        map[[.."asm"u8]] = X86AsmLexer.Instance;
        map[[.."nasm"u8]] = X86AsmLexer.Instance;
        map[[.."gas"u8]] = X86AsmLexer.Instance;
        map[[.."x86"u8]] = X86AsmLexer.Instance;
        map[[.."x86asm"u8]] = X86AsmLexer.Instance;
        map[[.."arm"u8]] = ArmAsmLexer.Instance;
        map[[.."armasm"u8]] = ArmAsmLexer.Instance;
        map[[.."aarch64"u8]] = ArmAsmLexer.Instance;
        map[[.."wat"u8]] = WatLexer.Instance;
        map[[.."wast"u8]] = WatLexer.Instance;
        map[[.."wasm"u8]] = WatLexer.Instance;
    }

    /// <summary>Builds the map of built-in language aliases.</summary>
    /// <returns>The built-in alias map.</returns>
    private static Dictionary<byte[], Lexer> BuildBuiltInAliasMap()
    {
        Dictionary<byte[], Lexer> map = [with (ByteArrayComparer.Instance)];
        RegisterCFamilyAliases(map);
        RegisterMarkupAliases(map);
        RegisterScriptingAliases(map);
        RegisterDataAliases(map);
        RegisterMiscAliases(map);
        RegisterFunctionalAliases(map);
        RegisterSchemaAliases(map);
        RegisterStylesheetAliases(map);
        RegisterBuildAliases(map);
        RegisterAsmAliases(map);
        return map;
    }

    /// <summary>Overlays <paramref name="extra"/> onto <paramref name="map"/> with last-write-wins semantics.</summary>
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

    /// <summary>Buckets <paramref name="map"/> by alias byte-length.</summary>
    /// <param name="map">Alias → lexer source.</param>
    /// <param name="extraParameterName">Name of the public parameter to cite on length-cap violation.</param>
    /// <returns>The length-bucketed alias and lexer tables.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <c>len &gt;= MaxAliasLength</c>.</exception>
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
                    BuildAliasTooLongMessage(kvp.Key, MaxAliasLength));
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
            var slot = cursors[len];
            cursors[len]++;
            aliases[len][slot] = key;
            lexers[len][slot] = value;
        }

        return (aliases, lexers);
    }

    /// <summary>Composes the alias-too-long exception message via the project's <see cref="StringCompose"/> helper (one explicit allocation per leaf concat).</summary>
    /// <param name="aliasBytes">The offending alias bytes (UTF-8).</param>
    /// <param name="cap">The byte-length cap that was exceeded.</param>
    /// <returns>Composed message.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildAliasTooLongMessage(byte[] aliasBytes, int cap) =>
        StringCompose.ConcatInt(
            StringCompose.Concat("Lexer alias '", Encoding.UTF8.GetString(aliasBytes), "' exceeds the "),
            cap,
            "-byte cap.");
}
