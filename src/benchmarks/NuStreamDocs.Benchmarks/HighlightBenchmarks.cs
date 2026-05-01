// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Highlight;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the syntax-highlight emitter, parameterised over the production lexers.</summary>
[MemoryDiagnoser]
public class HighlightBenchmarks
{
    /// <summary>Number of times each fixture line is repeated to give the lexer enough work to time.</summary>
    private const int Repetitions = 30;

    /// <summary>Pre-built C# fixture.</summary>
    private string _csharp = string.Empty;

    /// <summary>Pre-built TypeScript fixture.</summary>
    private string _typescript = string.Empty;

    /// <summary>Pre-built HTML fixture.</summary>
    private string _html = string.Empty;

    /// <summary>Pre-built XML fixture.</summary>
    private string _xml = string.Empty;

    /// <summary>Pre-built Razor fixture.</summary>
    private string _razor = string.Empty;

    /// <summary>Pre-built JSON fixture.</summary>
    private string _json = string.Empty;

    /// <summary>Pre-built YAML fixture.</summary>
    private string _yaml = string.Empty;

    /// <summary>Pre-built Bash fixture.</summary>
    private string _bash = string.Empty;

    /// <summary>Generates the per-lexer code fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _csharp = Repeat("public sealed class Foo { public int Bar(int x) => x + 1; /* comment */ string s = \"hi\"; }\n", Repetitions);
        _typescript = Repeat("export interface Bar { readonly id: number; readonly name: string; } const x: Bar = { id: 1, name: \"a\" };\n", Repetitions);
        _html = Repeat("<div class=\"x\"><a href=\"https://x.test\">link</a><img src=\"/y.png\" alt=\"y\"></div>\n", Repetitions);
        _xml = Repeat("<note id=\"a\"><title>Hello</title><body><![CDATA[x < y]]></body></note>\n", Repetitions);
        _razor = Repeat("<p>@Model.Name</p>\n@{ var x = 42; if (x > 0) { <span>@x</span> } }\n", Repetitions);
        _json = Repeat("{\"id\":1,\"name\":\"a\",\"items\":[1,2,3,true,null,{\"nested\":\"yes\"}]}\n", Repetitions);
        _yaml = Repeat("name: example\nitems:\n  - id: 1\n    label: \"first\"\n  - id: 2\n    label: \"second\"\n", Repetitions);
        _bash = Repeat("#!/usr/bin/env bash\nset -euo pipefail\nfor f in *.txt; do echo \"$f\"; done\n", Repetitions);
    }

    /// <summary>Benchmark: C# lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int CSharp() => Run(CSharpLexer.Instance, _csharp);

    /// <summary>Benchmark: TypeScript lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int TypeScript() => Run(TypeScriptLexer.Instance, _typescript);

    /// <summary>Benchmark: HTML lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Html() => Run(HtmlLexer.Instance, _html);

    /// <summary>Benchmark: XML lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Xml() => Run(XmlLexer.Instance, _xml);

    /// <summary>Benchmark: Razor lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Razor() => Run(RazorLexer.Instance, _razor);

    /// <summary>Benchmark: JSON lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Json() => Run(JsonLexer.Instance, _json);

    /// <summary>Benchmark: YAML lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Yaml() => Run(YamlLexer.Instance, _yaml);

    /// <summary>Benchmark: Bash lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Bash() => Run(BashLexer.Instance, _bash);

    /// <summary>Repeats <paramref name="line"/> <paramref name="count"/> times.</summary>
    /// <param name="line">Single source line.</param>
    /// <param name="count">Repetition count.</param>
    /// <returns>The concatenated string.</returns>
    private static string Repeat(string line, int count)
    {
        var builder = new StringBuilder(line.Length * count);
        for (var i = 0; i < count; i++)
        {
            builder.Append(line);
        }

        return builder.ToString();
    }

    /// <summary>Drives <c>HighlightEmitter.Emit</c> with <paramref name="lexer"/> against <paramref name="source"/>.</summary>
    /// <param name="lexer">Lexer under test.</param>
    /// <param name="source">Source text.</param>
    /// <returns>Bytes written.</returns>
    private static int Run(Lexer lexer, string source)
    {
        var sink = new ArrayBufferWriter<byte>(source.Length * 2);
        HighlightEmitter.Emit(lexer, source, sink);
        return sink.WrittenCount;
    }
}
