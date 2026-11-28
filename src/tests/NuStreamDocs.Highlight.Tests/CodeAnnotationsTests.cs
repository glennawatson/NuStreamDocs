// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Tests for line numbers, line highlights, inline diff/focus directives, and numbered callout annotations.</summary>
public sealed class CodeAnnotationsTests
{
    /// <summary>Initial output buffer capacity for test renders.</summary>
    private const int InitialCapacity = 256;

    /// <summary>Custom start line number used in line-numbering tests.</summary>
    private const int CustomStartLine = 10;

    /// <summary>Expected highlighted line 2.</summary>
    private const int SecondLine = 2;

    /// <summary>Expected highlighted line 4.</summary>
    private const int FourthLine = 4;

    /// <summary>Maximum clamped start line number.</summary>
    private const int ClampedMaxStartLine = 10_000;

    /// <summary>Verifies <c>linenums</c>, <c>hl_lines</c>, and <c>{...}</c> brace highlight ranges render expected line spans.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FenceAttributes_LineNumbersAndHighlightRanges_EmitLineSpansAndGutter()
    {
        var html = "<pre><code class=\"language-csharp\" data-info=\"linenums=&quot;10&quot; hl_lines=&quot;1&quot; {3-4}\">int a = 1;\nint b = 2;\nint c = 3;\nint d = 4;\n</code></pre>"u8;
        var rendered = Render(new(), html);

        await Assert.That(rendered).Contains("<span class=\"nd-line hll\"><span class=\"linenos\" data-line=\"10\"></span>");
        await Assert.That(rendered).Contains("<span class=\"nd-line\"><span class=\"linenos\" data-line=\"11\"></span>");
        await Assert.That(rendered).Contains("<span class=\"nd-line hll\"><span class=\"linenos\" data-line=\"12\"></span>");
        await Assert.That(rendered).Contains("<span class=\"nd-line hll\"><span class=\"linenos\" data-line=\"13\"></span>");
        await Assert.That(rendered).DoesNotContain("data-line=\"14\"");
    }

    /// <summary>Verifies global <see cref="HighlightOptions.LineNumbers"/> applies to both known, unknown, and auto-detected blocks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GlobalLineNumbers_AppliesToUnknownAndAutoDetectedBlocks()
    {
        var plugin = new HighlightPlugin(HighlightOptions.Default with
        {
            LineNumbers = true,
            AutoDetectLanguage = true,
        });

        var unknownRendered = Render(plugin, "<pre><code class=\"language-unknown\">plain text</code></pre>"u8);
        await Assert.That(unknownRendered).Contains("<span class=\"linenos\" data-line=\"1\"></span>plain text");

        var autoRendered = Render(plugin, "<pre><code>using System;\nnamespace Demo { public class Foo { private int _x; } }</code></pre>"u8);
        await Assert.That(autoRendered).Contains("<span class=\"linenos\" data-line=\"1\"></span>");
        await Assert.That(autoRendered).Contains("<span class=\"linenos\" data-line=\"2\"></span>");
    }

    /// <summary>Verifies <c>// [!code ++]</c>, <c>// [!code --]</c>, and <c># [!code focus]</c> directives strip comments and add line classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InlineDirectives_DiffAndFocusComments_StripMarkersAndStyleWrapper()
    {
        var csharpHtml = "<pre><code class=\"language-csharp\">int x = 1; // [!code --]\r\n\nint x = 2; // [!code ++]\n// [!code focus]\n</code></pre>"u8;
        var renderedCSharp = Render(new(), csharpHtml);

        await Assert.That(renderedCSharp).Contains("<div class=\"highlight nd-has-focus\">");
        await Assert.That(renderedCSharp).Contains("nd-line nd-line--del");
        await Assert.That(renderedCSharp).Contains("nd-line nd-line--add");
        await Assert.That(renderedCSharp).Contains("nd-line nd-line--focus");
        await Assert.That(renderedCSharp).DoesNotContain("[!code");

        var pythonHtml = "<pre><code class=\"language-python\">value = 1\t# [!code ++]\n# [!code --]</code></pre>"u8;
        var renderedPython = Render(new(), pythonHtml);

        await Assert.That(renderedPython).Contains("nd-line nd-line--add");
        await Assert.That(renderedPython).Contains("nd-line nd-line--del");
        await Assert.That(renderedPython).DoesNotContain("[!code");

        var emptyNumbered = Render(new(), "<pre><code class=\"language-csharp\" data-info=\"linenums=1\"></code></pre>"u8);
        await Assert.That(emptyNumbered).Contains("data-line=\"1\"");
    }

    /// <summary>Verifies multi-line tokens such as C# block comments split cleanly across annotated lines without breaking HTML nesting.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultiLineTokens_SplitCleanlyAcrossAnnotatedLines()
    {
        var html = "<pre><code class=\"language-csharp\" data-info=\"linenums=1\">/* line one\nline two */\nint y = 0;</code></pre>"u8;
        var rendered = Render(new(), html);

        await Assert.That(rendered).Contains("<span class=\"linenos\" data-line=\"1\"></span><span class=\"cm\">/* line one</span></span>\n");
        await Assert.That(rendered).Contains("<span class=\"linenos\" data-line=\"2\"></span><span class=\"cm\">line two */</span></span>\n");
        await Assert.That(rendered).Contains("<span class=\"linenos\" data-line=\"3\"></span>");
    }

    /// <summary>Verifies numbered callouts emit badges and upgrade a trailing <c>&lt;ol&gt;</c> into <c>nd-code-annotations</c>, while leaving non-list suffixes untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CalloutMarkers_EmitBadgesAndUpgradeTrailingOrderedList()
    {
        var withList = "<pre><code class=\"language-csharp\">var a = 1; // (1)!\nvar b = 2; # (2)!</code></pre>\n<ol><li>First</li><li>Second</li></ol>"u8;
        var renderedWithList = Render(new(), withList);

        await Assert.That(renderedWithList).Contains("<span class=\"nd-code-callout\" data-callout=\"1\" aria-label=\"Annotation 1\">1</span>");
        await Assert.That(renderedWithList).Contains("<span class=\"nd-code-callout\" data-callout=\"2\" aria-label=\"Annotation 2\">2</span>");
        await Assert.That(renderedWithList).Contains("<ol class=\"nd-code-annotations\"><li>First</li>");
        await Assert.That(renderedWithList).DoesNotContain("(1)!");

        var withoutList = "<pre><code class=\"language-csharp\">var a = 1; // (1)!</code></pre>\n<p>Not a list</p>"u8;
        var renderedWithoutList = Render(new(), withoutList);
        await Assert.That(renderedWithoutList).Contains("data-callout=\"1\"");
        await Assert.That(renderedWithoutList).Contains("<p>Not a list</p>");

        var atEndOfDoc = "<pre><code class=\"language-csharp\">var a = 1; // (1)!</code></pre>   "u8;
        var renderedAtEnd = Render(new(), atEndOfDoc);
        await Assert.That(renderedAtEnd).EndsWith("</pre></div>   ");
    }

    /// <summary>Verifies false-positive marker strings and disabled annotations fall back cleanly without corrupting code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FalsePositiveMarkersAndDisabledAnnotations_PreserveSourceText()
    {
        var falsePositives = """
            <pre><code class="language-csharp">var s = "https://a.test/[!code ++]";
            var h = "color:#[!code --]";
            // [!code unknown]
            // (0)!
            // (100)!
            // (abc)!
            // ()!
            </code></pre>
            """u8;
        var renderedFalsePositives = Render(new(), falsePositives);

        await Assert.That(renderedFalsePositives).DoesNotContain("nd-line");
        await Assert.That(renderedFalsePositives).Contains("[!code unknown]");

        var falsePositiveWithLineNums = "<pre><code class=\"language-csharp\" data-info=\"linenums=1\">// [!code unknown]</code></pre>"u8;
        var renderedFpWithLineNums = Render(new(), falsePositiveWithLineNums);
        await Assert.That(renderedFpWithLineNums).Contains("data-line=\"1\"");
        await Assert.That(renderedFpWithLineNums).Contains("[!code unknown]");

        var disabledAnnotations = new HighlightPlugin(HighlightOptions.Default with { EnableAnnotations = false });
        var renderedDisabled = Render(disabledAnnotations, "<pre><code class=\"language-csharp\">int x = 1; // [!code ++]</code></pre>"u8);
        await Assert.That(renderedDisabled).DoesNotContain("nd-line--add");
        await Assert.That(renderedDisabled).Contains("[!code ++]");
    }

    /// <summary>Verifies <see cref="FenceAttrParser.TryGetBraceHighlightLines"/>, <see cref="LineHighlightSet"/>, and <see cref="DocBuilderHighlightExtensions"/> edge cases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParserAndOptionHelpers_CoverMalformedAndBoundaryInputs()
    {
        await Assert.That(FenceAttrParser.TryGetBraceHighlightLines("title=\"{1,2}\" 'unclosed"u8, out _)).IsFalse();
        await Assert.That(FenceAttrParser.TryGetBraceHighlightLines("{} {unclosed"u8, out _)).IsFalse();

        var found = FenceAttrParser.TryGetBraceHighlightLines("title=\"{skip}\" {2, 4-5}"u8, out var rangeVal);
        var rangeText = Encoding.UTF8.GetString(rangeVal);
        await Assert.That(found).IsTrue();
        await Assert.That(rangeText).IsEqualTo("2, 4-5");

        HashSet<int>? set = null;
        LineHighlightSet.ParseInto(" , \t bad 0 20000 5-3 1-bad bad-2"u8, ref set);
        await Assert.That(set).IsNull();

        LineHighlightSet.ParseInto("2, 4-5"u8, ref set);
        await Assert.That(set).IsNotNull();
        await Assert.That(set!.Contains(SecondLine)).IsTrue();
        await Assert.That(set.Contains(FourthLine)).IsTrue();

        HashSet<int>? rangeFirstSet = null;
        LineHighlightSet.ParseInto("1-2, 4"u8, ref rangeFirstSet);
        await Assert.That(rangeFirstSet).IsNotNull();
        await Assert.That(rangeFirstSet!.Contains(1)).IsTrue();

        await Assert.That(LineHighlightSet.ParseStartLine("0"u8)).IsEqualTo(1);
        await Assert.That(LineHighlightSet.ParseStartLine("abc"u8)).IsEqualTo(1);
        await Assert.That(LineHighlightSet.ParseStartLine("99999"u8)).IsEqualTo(ClampedMaxStartLine);
        await Assert.That(LineHighlightSet.ParseStartLine("10"u8)).IsEqualTo(CustomStartLine);

        var configuredBuilder = new DocBuilder().UseHighlight(static opts => opts with { LineNumbers = true });
        await Assert.That(configuredBuilder).IsNotNull();

        Func<HighlightOptions, HighlightOptions>? nullConfigure = null;
        await Assert.That(() => new DocBuilder().UseHighlight(nullConfigure!)).Throws<ArgumentNullException>();

        var plugin = new HighlightPlugin(HighlightOptions.Default with { AutoDetectLanguage = true });
        var assets = plugin.StaticAssets;
        await Assert.That(assets.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(assets[0].Bytes)).Contains(".nd-code-callout");
        await Assert.That(Encoding.UTF8.GetString(HighlightStylesheet.AssetHref)).IsEqualTo("/assets/stylesheets/highlight.css");

        await Assert.That(Render(plugin, "<pre><code class=\"language-csharp\">unclosed"u8)).IsEqualTo("<pre><code class=\"language-csharp\">unclosed");
        await Assert.That(Render(plugin, "<pre><code class=\"language-csharp\">x</code>unclosed"u8)).IsEqualTo("<pre><code class=\"language-csharp\">x</code>unclosed");
        await Assert.That(Render(plugin, "<pre><code>unclosed"u8)).IsEqualTo("<pre><code>unclosed");
    }

    /// <summary>Renders HTML through <see cref="HighlightPlugin.PostRender"/> and returns the UTF-8 string.</summary>
    /// <param name="plugin">Plugin instance.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rendered HTML string.</returns>
    private static string Render(HighlightPlugin plugin, ReadOnlySpan<byte> html)
    {
        var output = new ArrayBufferWriter<byte>(InitialCapacity);
        PagePostRenderContext context = new("doc.md", default, html, output);
        plugin.PostRender(in context);
        return Encoding.UTF8.GetString(output.WrittenSpan);
    }
}
