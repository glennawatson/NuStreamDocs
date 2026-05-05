// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Highlight;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the syntax-highlight emitter, parameterized over the production lexers.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class HighlightBenchmarks
{
    /// <summary>Number of times each fixture line is repeated to give the lexer enough work to time.</summary>
    private const int Repetitions = 30;

    /// <summary>Headroom factor for the highlighted-output writer (each token wraps in <c>&lt;span class="..."&gt;...&lt;/span&gt;</c>; 4× covers token-dense input with margin).</summary>
    private const int OutputExpansionFactor = 4;

    /// <summary>Pre-built C# fixture.</summary>
    private byte[] _csharp = [];

    /// <summary>Pre-built TypeScript fixture.</summary>
    private byte[] _typescript = [];

    /// <summary>Pre-built HTML fixture.</summary>
    private byte[] _html = [];

    /// <summary>Pre-built XML fixture.</summary>
    private byte[] _xml = [];

    /// <summary>Pre-built Razor fixture.</summary>
    private byte[] _razor = [];

    /// <summary>Pre-built JSON fixture.</summary>
    private byte[] _json = [];

    /// <summary>Pre-built YAML fixture.</summary>
    private byte[] _yaml = [];

    /// <summary>Pre-built Bash fixture.</summary>
    private byte[] _bash = [];

    /// <summary>Pre-built Python fixture.</summary>
    private byte[] _python = [];

    /// <summary>Pre-built F# fixture.</summary>
    private byte[] _fsharp = [];

    /// <summary>Pre-built JavaScript fixture.</summary>
    private byte[] _javascript = [];

    /// <summary>Pre-built PowerShell fixture.</summary>
    private byte[] _powershell = [];

    /// <summary>Pre-built Diff fixture.</summary>
    private byte[] _diff = [];

    /// <summary>Generates the per-lexer code fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _csharp = Repeat("public sealed class Foo { public int Bar(int x) => x + 1; /* comment */ string s = \"hi\"; }\n"u8, Repetitions);
        _typescript = Repeat("export interface Bar { readonly id: number; readonly name: string; } const x: Bar = { id: 1, name: \"a\" };\n"u8, Repetitions);
        _html = Repeat("<div class=\"x\"><a href=\"https://x.test\">link</a><img src=\"/y.png\" alt=\"y\"></div>\n"u8, Repetitions);
        _xml = Repeat("<note id=\"a\"><title>Hello</title><body><![CDATA[x < y]]></body></note>\n"u8, Repetitions);
        _razor = Repeat("<p>@Model.Name</p>\n@{ var x = 42; if (x > 0) { <span>@x</span> } }\n"u8, Repetitions);
        _json = Repeat("{\"id\":1,\"name\":\"a\",\"items\":[1,2,3,true,null,{\"nested\":\"yes\"}]}\n"u8, Repetitions);
        _yaml = Repeat("name: example\nitems:\n  - id: 1\n    label: \"first\"\n  - id: 2\n    label: \"second\"\n"u8, Repetitions);
        _bash = Repeat("#!/usr/bin/env bash\nset -euo pipefail\nfor f in *.txt; do echo \"$f\"; done\n"u8, Repetitions);
        _python = Repeat("def hello(name: str) -> None:\n    \"\"\"Greet someone.\"\"\"\n    print(f\"Hello, {name}!\")\n\nif __name__ == \"__main__\":\n    hello(\"world\")\n"u8, Repetitions);
        _fsharp = Repeat("open System\nlet add x y = x + y\nlet result = [1; 2; 3] |> List.map (fun n -> n * n) |> List.sum\nprintfn \"%d\" result\n"u8, Repetitions);
        _javascript = Repeat("const items = [1, 2, 3];\nfunction sum(xs) { return xs.reduce((a, b) => a + b, 0); }\nconsole.log(sum(items));\n"u8, Repetitions);
        _powershell = Repeat("Get-ChildItem -Path . -Recurse | Where-Object { $_.Length -gt 1024 } | ForEach-Object { Write-Host $_.FullName }\n"u8, Repetitions);
        _diff = Repeat("--- a/file.txt\n+++ b/file.txt\n@@ -1,3 +1,3 @@\n removed line context\n-old line\n+new line\n still context\n"u8, Repetitions);
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

    /// <summary>Benchmark: Python lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Python() => Run(PythonLexer.Instance, _python);

    /// <summary>Benchmark: F# lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int FSharp() => Run(FSharpLexer.Instance, _fsharp);

    /// <summary>Benchmark: JavaScript lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int JavaScript() => Run(JavaScriptLexer.Instance, _javascript);

    /// <summary>Benchmark: PowerShell lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PowerShell() => Run(PowerShellLexer.Instance, _powershell);

    /// <summary>Benchmark: Diff / patch lexer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Diff() => Run(DiffLexer.Instance, _diff);

    /// <summary>Repeats <paramref name="line"/> <paramref name="count"/> times into a fresh byte array.</summary>
    /// <param name="line">Single source line.</param>
    /// <param name="count">Repetition count.</param>
    /// <returns>The concatenated UTF-8 bytes.</returns>
    private static byte[] Repeat(ReadOnlySpan<byte> line, int count)
    {
        var output = new byte[line.Length * count];
        var span = output.AsSpan();
        for (var i = 0; i < count; i++)
        {
            line.CopyTo(span[(i * line.Length)..]);
        }

        return output;
    }

    /// <summary>Drives <c>HighlightEmitter.Emit</c> with <paramref name="lexer"/> against <paramref name="source"/>; rents the sink from <see cref="PageBuilderPool"/> to mirror production.</summary>
    /// <param name="lexer">Lexer under test.</param>
    /// <param name="source">Source bytes.</param>
    /// <returns>Bytes written.</returns>
    private static int Run(Lexer lexer, byte[] source)
    {
        using var rental = PageBuilderPool.Rent(source.Length * OutputExpansionFactor);
        HighlightEmitter.Emit(lexer, source, rental.Writer);
        return rental.Writer.WrittenCount;
    }
}
