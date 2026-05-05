// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for bash, json, yaml, diff, and razor — the lexers needed for the rxui corpus's first release.</summary>
public class AdditionalLanguagesTests
{
    /// <summary>Bash classifies <c>if</c>/<c>then</c> as keywords and <c>$VAR</c> as a name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BashClassifiesKeywordsAndVariables()
    {
        var html = BashLexer.Instance.Render("if [ \"$x\" = 1 ]; then echo $HOME; fi"u8);
        await Assert.That(html.Contains("<span class=\"k\">if</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">then</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">fi</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">echo</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("$HOME", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>JSON classifies object keys as <c>na</c> and string values as <c>s2</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonClassifiesKeysAndValues()
    {
        var html = JsonLexer.Instance.Render("{ \"name\": \"Alice\", \"count\": 42, \"on\": true }"u8);
        await Assert.That(html.Contains("<span class=\"na\">&quot;name&quot;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">&quot;Alice&quot;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">42</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">true</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>YAML classifies plain mapping keys as <c>na</c> and constants as <c>kc</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task YamlClassifiesKeysAndConstants()
    {
        var html = YamlLexer.Instance.Render("site_name: ReactiveUI\nstrict: true\n"u8);
        await Assert.That(html.Contains("<span class=\"na\">site_name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">true</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Diff classifies hunk headers, additions, and deletions to the Generic.* CSS classes (<c>gi</c> / <c>gd</c> / <c>gu</c>).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiffClassifiesAddedRemovedAndHunkHeader()
    {
        var html = DiffLexer.Instance.Render("@@ -1 +1 @@\n-old\n+new\n"u8);
        await Assert.That(html.Contains("<span class=\"gu\">@@ -1 +1 @@</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"gi\">+new</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"gd\">-old</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Diff classifies file-header lines as <see cref="TokenClass.DiffFileHeader"/> (<c>gh</c>) — covers <c>---</c>, <c>+++</c>, <c>diff …</c>, <c>index …</c>, <c>Only in …</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiffClassifiesFileHeader()
    {
        var html = DiffLexer.Instance.Render("--- a/Old.cs\n+++ b/New.cs\n@@ -1 +1 @@\n"u8);
        await Assert.That(html.Contains("<span class=\"gh\">--- a/Old.cs</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"gh\">+++ b/New.cs</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Razor classifies inline <c>@var</c> expressions and switches into C# inside <c>@{ … }</c> blocks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RazorClassifiesInlineExpressionAndCsharpBlock()
    {
        var html = RazorLexer.Instance.Render("<p>@Model.Name</p>\n@{ var x = 42; }\n"u8);
        await Assert.That(html.Contains("<span class=\"n\">@Model.Name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">var</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">42</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>F# classifies the <c>let</c>/<c>fun</c>/<c>match</c> keywords, identifiers, and the pipe operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FSharpClassifiesKeywordsAndPipe()
    {
        var html = FSharpLexer.Instance.Render("let square x = x * x\nlet result = [1; 2; 3] |> List.map square\n"u8);
        await Assert.That(html.Contains("<span class=\"k\">let</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">|&gt;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">List</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>F# classifies <c>fun</c> lambda syntax (the rxui homepage hero sample) and the <c>-&gt;</c> arrow operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FSharpClassifiesFunLambda()
    {
        var html = FSharpLexer.Instance.Render("this.WhenAnyValue(fun x -> x.SearchQuery)"u8);
        await Assert.That(html.Contains("<span class=\"k\">fun</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">-&gt;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">WhenAnyValue</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>F# classifies <c>true</c>/<c>false</c>/<c>null</c> as constants and the F# primitive type names (<c>bool</c>, <c>string</c>, <c>list</c>, …) as type keywords.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FSharpClassifiesConstantsAndPrimitives()
    {
        var html = FSharpLexer.Instance.Render("let active : bool = true\nlet items : list = []\n"u8);
        await Assert.That(html.Contains("<span class=\"kc\">true</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">bool</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">list</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>F# block-comment <c>(* ... *)</c>, line comment <c>//</c>, and doc comment <c>///</c> all classify into the appropriate comment subclasses.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FSharpClassifiesAllCommentStyles()
    {
        var html = FSharpLexer.Instance.Render("/// doc\n// line\n(* block *)\nlet x = 1"u8);
        await Assert.That(html.Contains("<span class=\"cs\">/// doc</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">// line</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cm\">(* block *)</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Python classifies <c>def</c>/<c>class</c> as keywords, <c>True</c>/<c>None</c> as constants, and built-in callables as <c>nb</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PythonClassifiesKeywordsConstantsAndBuiltins()
    {
        var html = PythonLexer.Instance.Render("def greet(name: str) -> None:\n    print(f\"Hello, {name}\")\n    return None\n"u8);
        await Assert.That(html.Contains("<span class=\"k\">def</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">print</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">str</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">None</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Python classifies triple-quoted docstrings as a single <c>s2</c> token, with embedded newlines preserved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PythonClassifiesTripleQuotedString()
    {
        var html = PythonLexer.Instance.Render("\"\"\"hello\nworld\"\"\""u8);
        await Assert.That(html.Contains("<span class=\"s2\">&quot;&quot;&quot;hello\nworld&quot;&quot;&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>PowerShell classifies <c>if</c>/<c>foreach</c> case-insensitively, <c>$variable</c> as a name, and <c>Get-ChildItem</c> verb-noun cmdlets as <c>nb</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PowerShellClassifiesKeywordsVariablesAndVerbNoun()
    {
        var html = PowerShellLexer.Instance.Render("If ($name -eq 'admin') { Get-ChildItem -Path \"C:\\\" }"u8);
        await Assert.That(html.Contains("<span class=\"k\">If</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">-eq</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">Get-ChildItem</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>PowerShell block comment <c>&lt;# ... #&gt;</c> classifies as <c>cm</c>; the line comment <c>#</c> classifies as <c>c1</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PowerShellClassifiesCommentStyles()
    {
        var html = PowerShellLexer.Instance.Render("<# block #>\n# line\n$x = 1\n"u8);
        await Assert.That(html.Contains("<span class=\"cm\">&lt;# block #&gt;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\"># line</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>F# word operators <c>and</c>/<c>or</c>/<c>not</c> classify as operators, not as identifiers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FSharpClassifiesWordOperators()
    {
        var html = FSharpLexer.Instance.Render("if not (a and b or c) then 1 else 0"u8);
        await Assert.That(html.Contains("<span class=\"o\">not</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">and</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">or</span>", StringComparison.Ordinal)).IsTrue();
    }
}
