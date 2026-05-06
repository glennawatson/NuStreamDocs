// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.CFamily;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Behavior tests for the C# lexer + emitter.</summary>
public class CSharpLexerTests
{
    /// <summary>A keyword followed by an identifier and string lights up with the short-form CSS classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsShortFormCssClassesForBasicTokens()
    {
        var html = CSharpLexer.Instance.Render("var name = \"hi\";"u8);
        await Assert.That(html.Contains("<span class=\"kd\">var</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">=</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">&quot;hi&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Single-line comments classify as <c>c1</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesLineComment()
    {
        var html = CSharpLexer.Instance.Render("// hello\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\">// hello</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Doc comments classify as <c>cs</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesDocComment()
    {
        var html = CSharpLexer.Instance.Render("/// summary\n"u8);
        await Assert.That(html.Contains("<span class=\"cs\">/// summary</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Type keywords classify as <c>kt</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesTypeKeyword()
    {
        var html = CSharpLexer.Instance.Render("int x = 42;"u8);
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">42</span>", StringComparison.Ordinal)).IsTrue();
    }
}
