// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.CFamily;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Tests covering the C# 11–15 syntax forms <see cref="CSharpLexer"/> recognizes.</summary>
public class CSharpModernFeaturesTests
{
    /// <summary><c>"""..."""</c> raw string literals are highlighted as a single string-double token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task RawStringLiteral_is_classified_as_string()
    {
        var html = CSharpLexer.Instance.Render("var x = \"\"\"hello\"\"\";"u8);
        await Assert.That(html.Contains("<span class=\"s2\">&quot;&quot;&quot;hello&quot;&quot;&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Multi-line raw strings span newlines without breaking the string token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task RawStringLiteral_spans_newlines()
    {
        var html = CSharpLexer.Instance.Render("var x = \"\"\"\n  multi-line\n  raw\n  \"\"\";"u8);
        await Assert.That(html.Contains("multi-line", StringComparison.Ordinal)).IsTrue();

        // The whole multi-line content should sit inside one s2 span.
        await Assert.That(html.Contains("</span>multi-line", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>UTF-8 string literals (<c>"text"u8</c>) consume the <c>u8</c> suffix as part of the string token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task Utf8StringLiteral_includes_suffix()
    {
        var html = CSharpLexer.Instance.Render("var bytes = \"hello\"u8;"u8);
        await Assert.That(html.Contains("<span class=\"s2\">&quot;hello&quot;u8</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Interpolated string <c>$"..."</c> is classified as a string-double token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task InterpolatedString_is_classified_as_string()
    {
        var html = CSharpLexer.Instance.Render("var s = $\"hello {name}\";"u8);
        await Assert.That(html.Contains("<span class=\"s2\">$&quot;hello {name}&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Raw interpolated string <c>$"""..."""</c> is classified as a string-double token.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task RawInterpolatedString_is_classified_as_string()
    {
        var html = CSharpLexer.Instance.Render("var s = $\"\"\"line: {value}\"\"\";"u8);
        await Assert.That(html.Contains("$&quot;&quot;&quot;line: {value}&quot;&quot;&quot;", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Double-dollar raw interpolated <c>$$"""..."""</c> is recognized.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task DoubleDollarRawInterpolatedString_is_classified_as_string()
    {
        var html = CSharpLexer.Instance.Render("var s = $$\"\"\"open: {{x}}\"\"\";"u8);
        await Assert.That(html.Contains("$$&quot;&quot;&quot;open: {{x}}&quot;&quot;&quot;", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>scoped</c> is highlighted as a declaration-keyword (parameter / local modifier).</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ScopedKeyword_is_recognized()
    {
        var html = CSharpLexer.Instance.Render("void M(scoped ref int x) { }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">scoped</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>init</c> is highlighted as a declaration-keyword.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task InitKeyword_is_recognized()
    {
        var html = CSharpLexer.Instance.Render("public int X { get; init; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">init</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>field</c> contextual keyword is highlighted as a keyword inside both arrow-body and block-body accessors.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task FieldKeyword_is_recognized_in_accessor_bodies()
    {
        var html = CSharpLexer.Instance.Render("public int X { get => field; set => field = value; }"u8);
        await Assert.That(html.Contains("<span class=\"k\">field</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">value</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>field</c> outside an accessor body is classified as a plain identifier, not a keyword.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task FieldIdentifier_outside_accessor_is_not_keyword()
    {
        var html = CSharpLexer.Instance.Render("void M() { var field = 1; }"u8);
        await Assert.That(html.Contains("<span class=\"k\">field</span>", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("<span class=\"n\">field</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>value</c> outside a setter body is classified as a plain identifier.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ValueIdentifier_outside_accessor_is_not_keyword()
    {
        var html = CSharpLexer.Instance.Render("void M() { var value = 42; }"u8);
        await Assert.That(html.Contains("<span class=\"k\">value</span>", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("<span class=\"n\">value</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>extension</c> declaration keyword for C# 14 extension blocks.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ExtensionKeyword_is_recognized()
    {
        var html = CSharpLexer.Instance.Render("extension(string str) { public bool IsEmpty => str.Length is 0; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">extension</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>union</c> declaration keyword for the discriminated-union proposal.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task UnionKeyword_is_recognized()
    {
        var html = CSharpLexer.Instance.Render("union Result { Ok, Error }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">union</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>allows</c> contextual keyword in <c>where T : allows ref struct</c> generic constraints.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task AllowsKeyword_is_recognized()
    {
        var html = CSharpLexer.Instance.Render("void M<T>() where T : allows ref struct { }"u8);
        await Assert.That(html.Contains("<span class=\"k\">allows</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>notnull</c> generic constraint.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task NotnullKeyword_is_recognized()
    {
        var html = CSharpLexer.Instance.Render("void M<T>() where T : notnull { }"u8);
        await Assert.That(html.Contains("<span class=\"k\">notnull</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>unmanaged</c> generic constraint.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task UnmanagedKeyword_is_recognized()
    {
        var html = CSharpLexer.Instance.Render("void M<T>() where T : unmanaged { }"u8);
        await Assert.That(html.Contains("<span class=\"k\">unmanaged</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary><c>dynamic</c> is highlighted as a built-in type keyword.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task DynamicKeyword_is_recognized_as_type()
    {
        var html = CSharpLexer.Instance.Render("dynamic x = 1;"u8);
        await Assert.That(html.Contains("<span class=\"kt\">dynamic</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Collection-expression syntax <c>[1, 2, 3]</c> is lexed as integers + punctuation (no special highlighting needed at lexer level).</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task CollectionExpression_punctuation_lexes_correctly()
    {
        var html = CSharpLexer.Instance.Render("int[] arr = [1, 2, 3];"u8);
        await Assert.That(html.Contains("<span class=\"mi\">1</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">2</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">3</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Primary constructor declaration: <c>public class Point(int X, int Y)</c>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task PrimaryConstructor_keywords_recognized()
    {
        var html = CSharpLexer.Instance.Render("public class Point(int X, int Y);"u8);
        await Assert.That(html.Contains("<span class=\"kd\">public</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
    }
}
