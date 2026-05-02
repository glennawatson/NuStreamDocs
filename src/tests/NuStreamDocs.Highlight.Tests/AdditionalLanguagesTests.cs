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

    /// <summary>Diff classifies hunk headers, additions, and deletions distinctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiffClassifiesAddedRemovedAndHunkHeader()
    {
        var html = DiffLexer.Instance.Render("@@ -1 +1 @@\n-old\n+new\n"u8);
        await Assert.That(html.Contains("<span class=\"cs\">@@ -1 +1 @@</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"se\">+new</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cp\">-old</span>", StringComparison.Ordinal)).IsTrue();
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
}
