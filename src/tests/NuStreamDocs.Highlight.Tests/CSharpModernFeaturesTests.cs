// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Tests covering the C# 11–15 syntax forms <see cref="CSharpLexer"/> recognises.</summary>
public class CSharpModernFeaturesTests
{
    /// <summary><c>"""..."""</c> raw string literals are highlighted as a single string-double token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task RawStringLiteral_is_classified_as_string()
    {
        var html = Render(CSharpLexer.Instance, "var x = \"\"\"hello\"\"\";");
        await Assert.That(html.Contains("<span class=\"s2\">&quot;&quot;&quot;hello&quot;&quot;&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Multi-line raw strings span newlines without breaking the string token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task RawStringLiteral_spans_newlines()
    {
        const string Source = "var x = \"\"\"\n  multi-line\n  raw\n  \"\"\";";
        var html = Render(CSharpLexer.Instance, Source);
        await Assert.That(html.Contains("multi-line", StringComparison.Ordinal)).IsTrue();

        // The whole multi-line content should sit inside one s2 span.
        await Assert.That(html.Contains("</span>multi-line", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>UTF-8 string literals (<c>"text"u8</c>) consume the <c>u8</c> suffix as part of the string token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task Utf8StringLiteral_includes_suffix()
    {
        var html = Render(CSharpLexer.Instance, "var bytes = \"hello\"u8;");
        await Assert.That(html.Contains("<span class=\"s2\">&quot;hello&quot;u8</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Interpolated string <c>$"..."</c> is classified as a string-double token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task InterpolatedString_is_classified_as_string()
    {
        var html = Render(CSharpLexer.Instance, "var s = $\"hello {name}\";");
        await Assert.That(html.Contains("<span class=\"s2\">$&quot;hello {name}&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Raw interpolated string <c>$"""..."""</c> is classified as a string-double token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task RawInterpolatedString_is_classified_as_string()
    {
        var html = Render(CSharpLexer.Instance, "var s = $\"\"\"line: {value}\"\"\";");
        await Assert.That(html.Contains("$&quot;&quot;&quot;line: {value}&quot;&quot;&quot;", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Double-dollar raw interpolated <c>$$"""..."""</c> is recognised.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task DoubleDollarRawInterpolatedString_is_classified_as_string()
    {
        var html = Render(CSharpLexer.Instance, "var s = $$\"\"\"open: {{x}}\"\"\";");
        await Assert.That(html.Contains("$$&quot;&quot;&quot;open: {{x}}&quot;&quot;&quot;", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>scoped</c> is highlighted as a declaration-keyword (parameter / local modifier).</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ScopedKeyword_is_recognised()
    {
        var html = Render(CSharpLexer.Instance, "void M(scoped ref int x) { }");
        await Assert.That(html.Contains("<span class=\"kd\">scoped</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>init</c> is highlighted as a declaration-keyword.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task InitKeyword_is_recognised()
    {
        var html = Render(CSharpLexer.Instance, "public int X { get; init; }");
        await Assert.That(html.Contains("<span class=\"kd\">init</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>field</c> contextual keyword is highlighted as a keyword inside both arrow-body and block-body accessors.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task FieldKeyword_is_recognised_in_accessor_bodies()
    {
        var html = Render(CSharpLexer.Instance, "public int X { get => field; set => field = value; }");
        await Assert.That(html.Contains("<span class=\"k\">field</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">value</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>field</c> outside an accessor body is classified as a plain identifier, not a keyword.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task FieldIdentifier_outside_accessor_is_not_keyword()
    {
        var html = Render(CSharpLexer.Instance, "void M() { var field = 1; }");
        await Assert.That(html.Contains("<span class=\"k\">field</span>", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("<span class=\"n\">field</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>value</c> outside a setter body is classified as a plain identifier.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ValueIdentifier_outside_accessor_is_not_keyword()
    {
        var html = Render(CSharpLexer.Instance, "void M() { var value = 42; }");
        await Assert.That(html.Contains("<span class=\"k\">value</span>", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("<span class=\"n\">value</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>extension</c> declaration keyword for C# 14 extension blocks.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ExtensionKeyword_is_recognised()
    {
        var html = Render(CSharpLexer.Instance, "extension(string str) { public bool IsEmpty => str.Length is 0; }");
        await Assert.That(html.Contains("<span class=\"kd\">extension</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>union</c> declaration keyword for the discriminated-union proposal.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task UnionKeyword_is_recognised()
    {
        var html = Render(CSharpLexer.Instance, "union Result { Ok, Error }");
        await Assert.That(html.Contains("<span class=\"kd\">union</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>allows</c> contextual keyword in <c>where T : allows ref struct</c> generic constraints.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task AllowsKeyword_is_recognised()
    {
        var html = Render(CSharpLexer.Instance, "void M<T>() where T : allows ref struct { }");
        await Assert.That(html.Contains("<span class=\"k\">allows</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>notnull</c> generic constraint.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task NotnullKeyword_is_recognised()
    {
        var html = Render(CSharpLexer.Instance, "void M<T>() where T : notnull { }");
        await Assert.That(html.Contains("<span class=\"k\">notnull</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>unmanaged</c> generic constraint.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task UnmanagedKeyword_is_recognised()
    {
        var html = Render(CSharpLexer.Instance, "void M<T>() where T : unmanaged { }");
        await Assert.That(html.Contains("<span class=\"k\">unmanaged</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>dynamic</c> is highlighted as a built-in type keyword.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task DynamicKeyword_is_recognised_as_type()
    {
        var html = Render(CSharpLexer.Instance, "dynamic x = 1;");
        await Assert.That(html.Contains("<span class=\"kt\">dynamic</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Collection-expression syntax <c>[1, 2, 3]</c> is lexed as integers + punctuation (no special highlighting needed at lexer level).</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task CollectionExpression_punctuation_lexes_correctly()
    {
        var html = Render(CSharpLexer.Instance, "int[] arr = [1, 2, 3];");
        await Assert.That(html.Contains("<span class=\"mi\">1</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">2</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">3</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Primary constructor declaration: <c>public class Point(int X, int Y)</c>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PrimaryConstructor_keywords_recognised()
    {
        var html = Render(CSharpLexer.Instance, "public class Point(int X, int Y);");
        await Assert.That(html.Contains("<span class=\"kd\">public</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Renders <paramref name="source"/> through <paramref name="lexer"/> and returns the resulting HTML.</summary>
    /// <param name="lexer">Configured lexer.</param>
    /// <param name="source">Source text.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(Lexer lexer, string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        HighlightEmitter.Emit(lexer, source, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
