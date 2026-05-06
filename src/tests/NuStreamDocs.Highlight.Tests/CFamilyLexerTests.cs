// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.CFamily;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the C-family lexers (Rust, Go, C, C++, Java, Kotlin) built on top of <c>CFamilyRules</c>.</summary>
public class CFamilyLexerTests
{
    /// <summary>Rust classifies <c>fn</c>/<c>let</c>/<c>mut</c> as declarations and primitive types as type keywords.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RustClassifiesKeywordsAndTypes()
    {
        var html = RustLexer.Instance.Render("fn main() { let mut x: i32 = 0; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">fn</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">let</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">mut</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">i32</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Rust raw-string literals (<c>r#"..."#</c>) classify as a single double-string token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RustClassifiesRawStrings()
    {
        var html = RustLexer.Instance.Render("let s = r#\"hello \"world\"\"#;"u8);
        await Assert.That(html.Contains("<span class=\"s2\">r#&quot;hello &quot;world&quot;&quot;#</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Go classifies <c>package</c>/<c>func</c> as declarations and <c>:=</c> as a multi-byte operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GoClassifiesDeclarationsAndOperators()
    {
        var html = GoLexer.Instance.Render("package main\nfunc add(a, b int) int { x := a + b; return x }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">package</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">func</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">:=</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Go backtick raw strings classify as a single double-string token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GoClassifiesBacktickRawString()
    {
        var html = GoLexer.Instance.Render("var s = `hello\nworld`"u8);
        await Assert.That(html.Contains("<span class=\"s2\">`hello\nworld`</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>C classifies <c>#include</c> as a preprocessor directive and <c>int</c> as a type keyword.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CClassifiesPreprocessorAndTypes()
    {
        var html = CLexer.Instance.Render("#include <stdio.h>\nint main(void) { return 0; }"u8);
        await Assert.That(html.Contains("<span class=\"cp\">", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">void</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">return</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>C++ classifies <c>nullptr</c> as a constant, <c>class</c> as a declaration, and <c>::</c> as a multi-byte operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CppClassifiesClassAndConstant()
    {
        var html = CppLexer.Instance.Render("class Foo { public: int* p = nullptr; }; std::string s;"u8);
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">public</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">nullptr</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">::</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>C++ raw-string <c>R"d(...)d"</c> classifies as a single double-string token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CppClassifiesRawString()
    {
        var html = CppLexer.Instance.Render("auto s = R\"d(hello)d\";"u8);
        await Assert.That(html.Contains("<span class=\"s2\">R&quot;d(hello)d&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Java classifies <c>public</c>/<c>class</c>/<c>static</c>/<c>void</c> with their respective short-form CSS classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JavaClassifiesClassAndModifiers()
    {
        var html = JavaLexer.Instance.Render("public class Foo { public static void main(String[] args) {} }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">public</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">static</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">void</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Java text-block <c>"""..."""</c> classifies as a single double-string token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JavaClassifiesTextBlock()
    {
        var html = JavaLexer.Instance.Render("var s = \"\"\"\nhello\n\"\"\";"u8);
        await Assert.That(html.Contains("<span class=\"s2\">&quot;&quot;&quot;\nhello\n&quot;&quot;&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Kotlin classifies <c>fun</c>/<c>val</c>/<c>data</c> as declarations and <c>?:</c> as a multi-byte operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task KotlinClassifiesDeclarationsAndElvis()
    {
        var html = KotlinLexer.Instance.Render("data class Foo(val name: String) { fun greet() = name ?: \"x\" }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">data</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">val</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">fun</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">?:</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Detector resolves an obvious Rust block by its signatures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetectorResolvesRust()
    {
        const string Source = "fn main() { let mut v: Vec<i32> = Vec::new(); v.push(1); println!(\"{:?}\", v); }";
        var escaped = System.Text.Encoding.UTF8.GetBytes(System.Net.WebUtility.HtmlEncode(Source));
        var resolved = LanguageDetector.TryDetect(escaped, LexerRegistry.Default, [], out var languageId);
        var languageString = System.Text.Encoding.UTF8.GetString(languageId);
        await Assert.That(resolved).IsTrue();
        await Assert.That(languageString).IsEqualTo("rust");
    }

    /// <summary>Detector resolves an obvious Go block by its signatures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetectorResolvesGo()
    {
        const string Source = "package main\nimport (\n  \"fmt\"\n)\nfunc main() {\n  x := 42\n  fmt.Println(x)\n}";
        var escaped = System.Text.Encoding.UTF8.GetBytes(System.Net.WebUtility.HtmlEncode(Source));
        var resolved = LanguageDetector.TryDetect(escaped, LexerRegistry.Default, [], out var languageId);
        var languageString = System.Text.Encoding.UTF8.GetString(languageId);
        await Assert.That(resolved).IsTrue();
        await Assert.That(languageString).IsEqualTo("go");
    }

    /// <summary>Detector resolves an obvious Java block by its signatures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetectorResolvesJava()
    {
        const string Source = "import java.util.*;\npublic class App {\n  public static void main(String[] args) {\n    System.out.println(\"hi\");\n  }\n}";
        var escaped = System.Text.Encoding.UTF8.GetBytes(System.Net.WebUtility.HtmlEncode(Source));
        var resolved = LanguageDetector.TryDetect(escaped, LexerRegistry.Default, [], out var languageId);
        var languageString = System.Text.Encoding.UTF8.GetString(languageId);
        await Assert.That(resolved).IsTrue();
        await Assert.That(languageString).IsEqualTo("java");
    }
}
