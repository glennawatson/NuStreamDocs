// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.CFamily;
using NuStreamDocs.Highlight.Languages.Functional;
using NuStreamDocs.Highlight.Languages.Scripting;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the second Phase-2 batch (Scala, Groovy, Dart, Objective-C, Zig, Elm, Common Lisp, Lua).</summary>
public class Phase2BatchLexerTests
{
    /// <summary>Scala classifies <c>def</c>/<c>val</c>/<c>class</c> as declarations, primitive types as <c>kt</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ScalaClassifiesKeywordsAndTypes()
    {
        var html = ScalaLexer.Instance.Render("class Foo(val name: String) { def greet(): Unit = println(name) }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">val</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">def</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">String</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">Unit</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Groovy classifies <c>def</c>, JVM modifiers, and triple-quoted strings.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroovyClassifiesDefAndTripleQuoted()
    {
        var html = GroovyLexer.Instance.Render("def name = \"\"\"hi\nworld\"\"\"\nclass Foo { static int x = 1 }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">def</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">&quot;&quot;&quot;hi\nworld&quot;&quot;&quot;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Dart classifies <c>final</c>/<c>const</c>/<c>class</c>, primitive types, and the <c>?:</c> Elvis operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DartClassifiesDeclarationsAndOperators()
    {
        var html = DartLexer.Instance.Render("final List<String> items = [];\nclass Foo { int? n; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">final</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">List</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Objective-C classifies <c>@interface</c> directives via the at-prefixed declaration set.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ObjectiveCClassifiesAtDirectivesAsDeclarations()
    {
        var html = ObjectiveCLexer.Instance.Render("#import &lt;Foundation/Foundation.h&gt;\n@interface Foo : NSObject\n@end"u8);
        await Assert.That(html.Contains("<span class=\"cp\">", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">@interface</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">@end</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Zig classifies <c>fn</c>/<c>pub</c>/<c>const</c>, primitive widths, and constants.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ZigClassifiesKeywordsAndPrimitives()
    {
        var html = ZigLexer.Instance.Render("pub fn main() void { const x: i32 = 42; if (x == 0) unreachable; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">pub</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">fn</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">const</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">i32</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">void</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">unreachable</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Elm classifies <c>module</c> as a declaration, <c>where</c> as a keyword, and Maybe / Result types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ElmClassifiesModuleAndTypeKeywords()
    {
        var html = ElmLexer.Instance.Render("module Main exposing (..)\n-- comment\ntype alias Model = { count : Int }\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">module</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">-- comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">type</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">alias</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">Int</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Common Lisp classifies <c>defun</c>/<c>defmacro</c> and the <c>nil</c>/<c>t</c> constants.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CommonLispClassifiesDefunAndConstants()
    {
        var html = CommonLispLexer.Instance.Render("(defun greet (name)\n  (when name\n    (format t \"hi ~a\" name)))"u8);
        await Assert.That(html.Contains("<span class=\"kd\">defun</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">when</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">t</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Lua classifies <c>function</c>/<c>local</c>/<c>end</c>, <c>--</c> line comments, and <c>--[[ ... ]]</c> block comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LuaClassifiesKeywordsAndComments()
    {
        var html = LuaLexer.Instance.Render("-- top\n--[[ block ]]\nlocal function f(x)\n  return x\nend"u8);
        await Assert.That(html.Contains("<span class=\"c1\">-- top</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cm\">--[[ block ]]</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">local</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">function</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">return</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">end</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Lua classifies <c>[[ ... ]]</c> long-string literals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LuaClassifiesLongString()
    {
        var html = LuaLexer.Instance.Render("local s = [[hello\nworld]]"u8);
        await Assert.That(html.Contains("<span class=\"s2\">[[hello\nworld]]</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the new aliases to their lexers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesNewAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "scala"u8], out var scala)).IsTrue();
        await Assert.That(scala).IsSameReferenceAs(ScalaLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "groovy"u8], out var groovy)).IsTrue();
        await Assert.That(groovy).IsSameReferenceAs(GroovyLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "gradle"u8], out var gradle)).IsTrue();
        await Assert.That(gradle).IsSameReferenceAs(GroovyLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "dart"u8], out var dart)).IsTrue();
        await Assert.That(dart).IsSameReferenceAs(DartLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "objc"u8], out var objc)).IsTrue();
        await Assert.That(objc).IsSameReferenceAs(ObjectiveCLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "zig"u8], out var zig)).IsTrue();
        await Assert.That(zig).IsSameReferenceAs(ZigLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "elm"u8], out var elm)).IsTrue();
        await Assert.That(elm).IsSameReferenceAs(ElmLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "commonlisp"u8], out var cl)).IsTrue();
        await Assert.That(cl).IsSameReferenceAs(CommonLispLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "lua"u8], out var lua)).IsTrue();
        await Assert.That(lua).IsSameReferenceAs(LuaLexer.Instance);
    }
}
