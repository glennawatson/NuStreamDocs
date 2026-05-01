// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Behaviour tests for the C# lexer + emitter.</summary>
public class CSharpLexerTests
{
    /// <summary>A keyword followed by an identifier and string lights up with Pygments classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsPygmentsClassesForBasicTokens()
    {
        var html = Render(CSharpLexer.Instance, "var name = \"hi\";");
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
        var html = Render(CSharpLexer.Instance, "// hello\n");
        await Assert.That(html.Contains("<span class=\"c1\">// hello</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Doc comments classify as <c>cs</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesDocComment()
    {
        var html = Render(CSharpLexer.Instance, "/// summary\n");
        await Assert.That(html.Contains("<span class=\"cs\">/// summary</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Type keywords classify as <c>kt</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesTypeKeyword()
    {
        var html = Render(CSharpLexer.Instance, "int x = 42;");
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
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
