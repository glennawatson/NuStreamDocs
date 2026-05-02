// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Behavior tests for the XML/HTML and TypeScript lexers.</summary>
public class XmlAndTypeScriptTests
{
    /// <summary>An XML tag classifies its name as <c>nc</c> and angle brackets as <c>p</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task XmlTagPartsAreClassified()
    {
        var html = XmlLexer.Instance.Render("<note id=\"a\">hi</note>"u8);
        await Assert.That(html.Contains("<span class=\"p\">&lt;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nc\">note</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">id</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">&quot;a&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>HTML lexer reuses the XML state map, so a comment classifies as <c>cm</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HtmlCommentClassifies()
    {
        var html = HtmlLexer.Instance.Render("<!-- comment -->\n"u8);
        await Assert.That(html.Contains("<span class=\"cm\">&lt;!-- comment --&gt;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>TypeScript classifies <c>const</c>, identifiers, template strings, and arrow operators.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TypeScriptClassifiesCoreTokens()
    {
        var html = TypeScriptLexer.Instance.Render("const greet = (name: string) => `hi ${name}`;"u8);
        await Assert.That(html.Contains("<span class=\"kd\">const</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">string</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">=&gt;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>The placeholder map registers names but emits plain text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlaceholderLanguagesResolve()
    {
        await Assert.That(LexerRegistry.Default.TryGet("rust"u8, out var rust)).IsTrue();
        await Assert.That(rust).IsNotNull();
        await Assert.That(rust!).IsAssignableTo<Lexer>();

        await Assert.That(LexerRegistry.Default.TryGet("py"u8, out var python)).IsTrue();
        await Assert.That(python!).IsNotNull();
        await Assert.That(python!).IsAssignableTo<Lexer>();
    }
}
