// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
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
        var html = Render(BashLexer.Instance, "if [ \"$x\" = 1 ]; then echo $HOME; fi");
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
        var html = Render(JsonLexer.Instance, "{ \"name\": \"Alice\", \"count\": 42, \"on\": true }");
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
        var html = Render(YamlLexer.Instance, "site_name: ReactiveUI\nstrict: true\n");
        await Assert.That(html.Contains("<span class=\"na\">site_name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">true</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Diff classifies hunk headers, additions, and deletions distinctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiffClassifiesAddedRemovedAndHunkHeader()
    {
        var html = Render(DiffLexer.Instance, "@@ -1 +1 @@\n-old\n+new\n");
        await Assert.That(html.Contains("<span class=\"cs\">@@ -1 +1 @@</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"se\">+new</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cp\">-old</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Razor classifies inline <c>@var</c> expressions and switches into C# inside <c>@{ … }</c> blocks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RazorClassifiesInlineExpressionAndCsharpBlock()
    {
        var html = Render(RazorLexer.Instance, "<p>@Model.Name</p>\n@{ var x = 42; }\n");
        await Assert.That(html.Contains("<span class=\"n\">@Model.Name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">var</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">42</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Renders <paramref name="source"/> through <paramref name="lexer"/> and decodes the result.</summary>
    /// <param name="lexer">Lexer.</param>
    /// <param name="source">Source code.</param>
    /// <returns>UTF-8-decoded HTML.</returns>
    private static string Render(Lexer lexer, string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        HighlightEmitter.Emit(lexer, source, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
