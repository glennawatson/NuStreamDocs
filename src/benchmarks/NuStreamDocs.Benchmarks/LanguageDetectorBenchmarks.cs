// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Highlight;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Throughput + allocation benchmarks for the auto-detect feature in <see cref="HighlightPlugin"/>.
/// </summary>
/// <remarks>
/// Three axes of measurement:
/// <list type="bullet">
/// <item><c>NeedsRewrite_*</c> — the per-page short-circuit probe; verifies the cost gap between
/// auto-detect off (single SIMD scan for labeled blocks) and on (two scans, labeled + unlabeled).</item>
/// <item><c>Detect_*</c> — the heuristic detector itself in isolation, against bodies of varying
/// size and language; with and without an allow-list scoping it.</item>
/// <item><c>PostRender_*</c> — end-to-end plugin rewrite path covering the four real-world shapes
/// (no code, labeled-only, unlabeled-confident-detect, unlabeled-no-confidence).</item>
/// </list>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class LanguageDetectorBenchmarks
{
    /// <summary>Body size used for the detector-isolation benchmarks.</summary>
    private const int DetectBodyRepeat = 12;

    /// <summary>Sink expansion factor for end-to-end PostRender benchmarks (auto-detect adds wrapper + lexed spans).</summary>
    private const int OutputExpansionFactor = 4;

    /// <summary>Plugin instance with auto-detect off (current default).</summary>
    private HighlightPlugin _pluginAutoDetectOff = null!;

    /// <summary>Plugin instance with auto-detect on, no allow-list (every registered language scored).</summary>
    private HighlightPlugin _pluginAutoDetectOn = null!;

    /// <summary>Plugin instance with auto-detect on, scoped to <c>csharp</c> + <c>powershell</c>.</summary>
    private HighlightPlugin _pluginAutoDetectScoped = null!;

    /// <summary>Pre-built C# detection body (HTML-escaped, what the detector actually sees).</summary>
    private byte[] _bodyCSharp = [];

    /// <summary>Pre-built PowerShell detection body.</summary>
    private byte[] _bodyPowerShell = [];

    /// <summary>Pre-built XML detection body.</summary>
    private byte[] _bodyXml = [];

    /// <summary>Pre-built HTML detection body.</summary>
    private byte[] _bodyHtml = [];

    /// <summary>Pre-built JSON detection body.</summary>
    private byte[] _bodyJson = [];

    /// <summary>Pre-built TypeScript detection body.</summary>
    private byte[] _bodyTypeScript = [];

    /// <summary>Pre-built JavaScript detection body.</summary>
    private byte[] _bodyJavaScript = [];

    /// <summary>Pre-built Python detection body.</summary>
    private byte[] _bodyPython = [];

    /// <summary>Pre-built Bash detection body.</summary>
    private byte[] _bodyBash = [];

    /// <summary>Pre-built YAML detection body.</summary>
    private byte[] _bodyYaml = [];

    /// <summary>Pre-built F# detection body.</summary>
    private byte[] _bodyFSharp = [];

    /// <summary>Pre-built Diff detection body.</summary>
    private byte[] _bodyDiff = [];

    /// <summary>Pre-built ambiguous prose body — should fail the confidence threshold.</summary>
    private byte[] _bodyAmbiguous = [];

    /// <summary>Pre-built page HTML containing only a labeled <c>language-csharp</c> block.</summary>
    private byte[] _pageLabeledOnly = [];

    /// <summary>Pre-built page HTML containing only an unlabeled C# block (matches strongly under detection).</summary>
    private byte[] _pageUnlabeledCSharp = [];

    /// <summary>Pre-built page HTML containing only an unlabeled prose block (no confident match).</summary>
    private byte[] _pageUnlabeledAmbiguous = [];

    /// <summary>Pre-built page HTML containing no code blocks at all (the no-op short-circuit case).</summary>
    private byte[] _pageNoCode = [];

    /// <summary>Built-in lexer registry — the same set <see cref="HighlightOptions.Default"/> ships with.</summary>
    private LexerRegistry _registry = null!;

    /// <summary>Empty allow-list — equivalent to "consider every registered language".</summary>
    private byte[][] _allowAll = [];

    /// <summary>Two-language allow-list — only csharp and powershell are scored.</summary>
    private byte[][] _allowScoped = [];

    /// <summary>Builds the per-axis fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        // Detector-isolation fixtures: HTML-escaped bodies as the plugin would pass them.
        _bodyCSharp = RepeatLine(
            "using System;\nnamespace Demo { public class Foo { private int _x; } }\n"u8,
            DetectBodyRepeat);
        _bodyPowerShell = RepeatLine(
            "Install-Package ReactiveUI.WPF\nGet-Item .\\foo\nWrite-Host hello\n"u8,
            DetectBodyRepeat);
        _bodyXml = RepeatLine(
            "&lt;?xml version=\"1.0\" encoding=\"UTF-8\"?&gt;\n&lt;root xmlns=\"x\"&gt;&lt;child/&gt;&lt;/root&gt;\n"u8,
            DetectBodyRepeat);
        _bodyHtml = RepeatLine(
            "&lt;!DOCTYPE html&gt;\n&lt;html&gt;&lt;head&gt;&lt;title&gt;t&lt;/title&gt;&lt;/head&gt;&lt;body&gt;&lt;div&gt;&lt;span&gt;hi&lt;/span&gt;&lt;/div&gt;&lt;/body&gt;&lt;/html&gt;\n"u8,
            DetectBodyRepeat);
        _bodyJson = RepeatLine(
            "{&quot;id&quot;:&quot;abc&quot;,&quot;count&quot;: 3,&quot;tags&quot;: [&quot;a&quot;,&quot;b&quot;],&quot;nested&quot;: {&quot;ok&quot;:&quot;yes&quot;}}\n"u8,
            DetectBodyRepeat);
        _bodyTypeScript = RepeatLine(
            "interface Item { id: number; name: string; active: boolean; }\nexport type Status = &quot;a&quot; | &quot;b&quot;;\nas const\n"u8,
            DetectBodyRepeat);
        _bodyJavaScript = RepeatLine(
            "const items = [1, 2, 3];\nfunction sum(xs) { return xs.reduce((a, b) =&gt; a + b, 0); }\nconsole.log(sum(items));\nmodule.exports = sum;\n"u8,
            DetectBodyRepeat);
        _bodyPython = RepeatLine(
            "from typing import List\nimport sys\nclass Foo:\n    def bar(self, items: List[int]) -&gt; int:\n        return sum(items)\nif __name__ == \"__main__\":\n    pass\n"u8,
            DetectBodyRepeat);
        _bodyBash = RepeatLine(
            "#!/usr/bin/env bash\nset -euo pipefail\nfor f in *.txt; do echo \"$f\"; done\nif [ -f readme.md ]; then echo found; fi\n"u8,
            DetectBodyRepeat);
        _bodyYaml = RepeatLine(
            "---\nname: example\nversion: 1.0\nitems:\n  - name: a\n  - name: b\n"u8,
            DetectBodyRepeat);
        _bodyFSharp = RepeatLine(
            "module Demo\nopen System\nlet add x y = x + y\nmatch result with | Some v -&gt; v |&gt; printfn \"%d\" | None -&gt; ()\n"u8,
            DetectBodyRepeat);
        _bodyDiff = RepeatLine(
            "--- a/file.txt\n+++ b/file.txt\n@@ -1,3 +1,3 @@\n context\n-old\n+new\n"u8,
            DetectBodyRepeat);
        _bodyAmbiguous = RepeatLine(
            "this is just plain prose with no code keywords whatsoever\n"u8,
            DetectBodyRepeat);

        _pageNoCode = [.. "<p>plain prose</p><p>more prose</p>"u8];
        _pageLabeledOnly = WrapInPreCode(_bodyCSharp, language: "csharp");
        _pageUnlabeledCSharp = WrapInPreCode(_bodyCSharp, language: null);
        _pageUnlabeledAmbiguous = WrapInPreCode(_bodyAmbiguous, language: null);

        _pluginAutoDetectOff = new(HighlightOptions.Default);
        _pluginAutoDetectOn = new(HighlightOptions.Default with { AutoDetectLanguage = true });
        _pluginAutoDetectScoped = new(HighlightOptions.Default with
        {
            AutoDetectLanguage = true,
            DetectionLanguages = [[.. "csharp"u8], [.. "powershell"u8]]
        });

        _registry = LexerRegistry.Build();
        _allowAll = [];
        _allowScoped = [[.. "csharp"u8], [.. "powershell"u8]];
    }

    /// <summary>
    /// NeedsRewrite on a page with no code blocks — auto-detect off (baseline single SIMD scan).
    /// </summary>
    /// <returns>Always false.</returns>
    [Benchmark]
    public bool NeedsRewrite_NoCode_AutoDetectOff() =>
        _pluginAutoDetectOff.NeedsRewrite(_pageNoCode);

    /// <summary>
    /// NeedsRewrite on a page with no code blocks — auto-detect on (extra union-scan for unlabeled openers).
    /// </summary>
    /// <returns>Always false.</returns>
    [Benchmark]
    public bool NeedsRewrite_NoCode_AutoDetectOn() =>
        _pluginAutoDetectOn.NeedsRewrite(_pageNoCode);

    /// <summary>NeedsRewrite when only a labeled block is present — auto-detect off.</summary>
    /// <returns>Always true.</returns>
    [Benchmark]
    public bool NeedsRewrite_LabeledOnly_AutoDetectOff() =>
        _pluginAutoDetectOff.NeedsRewrite(_pageLabeledOnly);

    /// <summary>NeedsRewrite when only an unlabeled block is present — auto-detect off (returns false, no work).</summary>
    /// <returns>Always false (auto-detect off ignores unlabeled blocks).</returns>
    [Benchmark]
    public bool NeedsRewrite_UnlabeledOnly_AutoDetectOff() =>
        _pluginAutoDetectOff.NeedsRewrite(_pageUnlabeledCSharp);

    /// <summary>NeedsRewrite when only an unlabeled block is present — auto-detect on (returns true).</summary>
    /// <returns>Always true.</returns>
    [Benchmark]
    public bool NeedsRewrite_UnlabeledOnly_AutoDetectOn() =>
        _pluginAutoDetectOn.NeedsRewrite(_pageUnlabeledCSharp);

    /// <summary>Detector cost on a strong-signal C# body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_CSharp_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyCSharp, _registry, _allowAll, out _);

    /// <summary>Detector cost on a strong-signal C# body, allow-list scoped to csharp + powershell.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_CSharp_AllowListScoped() =>
        LanguageDetector.TryDetect(_bodyCSharp, _registry, _allowScoped, out _);

    /// <summary>Detector cost on a strong-signal PowerShell body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_PowerShell_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyPowerShell, _registry, _allowAll, out _);

    /// <summary>Detector cost on an XML body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_Xml_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyXml, _registry, _allowAll, out _);

    /// <summary>Detector cost on an HTML body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_Html_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyHtml, _registry, _allowAll, out _);

    /// <summary>Detector cost on a JSON body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_Json_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyJson, _registry, _allowAll, out _);

    /// <summary>Detector cost on a TypeScript body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_TypeScript_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyTypeScript, _registry, _allowAll, out _);

    /// <summary>Detector cost on a JavaScript body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_JavaScript_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyJavaScript, _registry, _allowAll, out _);

    /// <summary>Detector cost on a Python body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_Python_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyPython, _registry, _allowAll, out _);

    /// <summary>Detector cost on a Bash body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_Bash_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyBash, _registry, _allowAll, out _);

    /// <summary>Detector cost on a YAML body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_Yaml_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyYaml, _registry, _allowAll, out _);

    /// <summary>Detector cost on an F# body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_FSharp_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyFSharp, _registry, _allowAll, out _);

    /// <summary>Detector cost on a Diff body, all profiles in scope.</summary>
    /// <returns>True (high confidence).</returns>
    [Benchmark]
    public bool Detect_Diff_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyDiff, _registry, _allowAll, out _);

    /// <summary>Detector cost on an ambiguous prose body — no confident winner.</summary>
    /// <returns>False (below confidence threshold).</returns>
    [Benchmark]
    public bool Detect_Ambiguous_AllProfiles() =>
        LanguageDetector.TryDetect(_bodyAmbiguous, _registry, _allowAll, out _);

    /// <summary>End-to-end PostRender on a page with no code blocks — auto-detect off (NeedsRewrite short-circuits before allocation).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PostRender_NoCode_AutoDetectOff() =>
        RunPostRender(_pluginAutoDetectOff, _pageNoCode);

    /// <summary>End-to-end PostRender on a page with no code blocks — auto-detect on (still short-circuits because no <c>&lt;pre&gt;&lt;code&gt;</c> opener exists).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PostRender_NoCode_AutoDetectOn() =>
        RunPostRender(_pluginAutoDetectOn, _pageNoCode);

    /// <summary>End-to-end PostRender on a labeled-only page — auto-detect off (existing labeled-block path; baseline).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PostRender_LabeledOnly_AutoDetectOff() =>
        RunPostRender(_pluginAutoDetectOff, _pageLabeledOnly);

    /// <summary>End-to-end PostRender on an unlabeled C# page — auto-detect on (detect → label → lex).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PostRender_UnlabeledCSharp_AutoDetectOn() =>
        RunPostRender(_pluginAutoDetectOn, _pageUnlabeledCSharp);

    /// <summary>End-to-end PostRender on an unlabeled C# page — auto-detect on, allow-list scoped to csharp + powershell.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PostRender_UnlabeledCSharp_AutoDetectScoped() =>
        RunPostRender(_pluginAutoDetectScoped, _pageUnlabeledCSharp);

    /// <summary>End-to-end PostRender on an unlabeled ambiguous page — auto-detect on, no confident match (block passes through verbatim).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PostRender_UnlabeledAmbiguous_AutoDetectOn() =>
        RunPostRender(_pluginAutoDetectOn, _pageUnlabeledAmbiguous);

    /// <summary>Repeats <paramref name="line"/> <paramref name="count"/> times into a fresh byte array.</summary>
    /// <param name="line">Fixture line.</param>
    /// <param name="count">Repetition count.</param>
    /// <returns>Concatenated bytes.</returns>
    private static byte[] RepeatLine(ReadOnlySpan<byte> line, int count)
    {
        var output = new byte[line.Length * count];
        var span = output.AsSpan();
        for (var i = 0; i < count; i++)
        {
            line.CopyTo(span[(i * line.Length)..]);
        }

        return output;
    }

    /// <summary>Wraps <paramref name="body"/> in <c>&lt;pre&gt;&lt;code&gt;…&lt;/code&gt;&lt;/pre&gt;</c>, optionally with a language class.</summary>
    /// <param name="body">Body bytes (used as-is; not HTML-escaped a second time — fixtures are already escape-safe).</param>
    /// <param name="language">Language alias for the <c>class="language-X"</c> attribute, or null for an unlabeled block.</param>
    /// <returns>Page-shaped HTML bytes.</returns>
    private static byte[] WrapInPreCode(byte[] body, string? language)
    {
        var prefix = language is null
            ? [.. "<p>intro</p>\n<pre><code>"u8]
            : System.Text.Encoding.UTF8.GetBytes($"<p>intro</p>\n<pre><code class=\"language-{language}\">");
        byte[] suffix = [.. "</code></pre>\n<p>outro</p>"u8];
        var output = new byte[prefix.Length + body.Length + suffix.Length];
        prefix.CopyTo(output, 0);
        body.CopyTo(output, prefix.Length);
        suffix.CopyTo(output, prefix.Length + body.Length);
        return output;
    }

    /// <summary>Drives one PostRender call against a fresh sink and returns the bytes-written count.</summary>
    /// <param name="plugin">Plugin instance.</param>
    /// <param name="html">Page-shaped input bytes.</param>
    /// <returns>Bytes written.</returns>
    private static int RunPostRender(HighlightPlugin plugin, byte[] html)
    {
        ArrayBufferWriter<byte> output = new(html.Length * OutputExpansionFactor);
        PagePostRenderContext ctx = new("p.md", default, html, output);
        plugin.PostRender(in ctx);
        return output.WrittenCount;
    }
}
