// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the Phase-0 family helpers (CssFamilyRules, MlFamilyRules, LispFamilyRules) and their consumers.</summary>
public class Phase0FamilyLexerTests
{
    /// <summary>CSS classifies selectors, properties, hex colours, and dimensioned numbers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CssClassifiesSelectorsAndProperties()
    {
        var html = CssLexer.Instance.Render(".btn-primary { color: #ff8800; padding: 12px 1.5em; }"u8);
        await Assert.That(html.Contains("<span class=\"nc\">.btn-primary</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">color</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mh\">#ff8800</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">12px</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mf\">1.5em</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>SCSS classifies <c>$variable</c>, <c>//</c> line comments, and the <c>&amp;</c> parent selector.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ScssClassifiesVariableAndParentSelector()
    {
        var html = ScssLexer.Instance.Render("$primary: #ff0;\n// inline comment\n.btn {\n  &:hover { color: $primary; }\n}\n"u8);
        await Assert.That(html.Contains("<span class=\"n\">$primary</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">// inline comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nc\">&amp;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Less classifies <c>@variable</c> as a keyword and accepts <c>//</c> line comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LessClassifiesVariableAndLineComment()
    {
        var html = LessLexer.Instance.Render("@brand: red;\n// less comment\n.btn { color: @brand; }\n"u8);
        await Assert.That(html.Contains("<span class=\"k\">@brand</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">// less comment</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>OCaml classifies nested block comments and ML keywords.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OcamlClassifiesNestedCommentsAndKeywords()
    {
        var html = OcamlLexer.Instance.Render("(* outer (* inner *) outer *)\nlet x : int = 1\nmatch x with\n| 0 -> false\n| _ -> true"u8);
        await Assert.That(html.Contains("<span class=\"cm\">(* outer (* inner *) outer *)</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">let</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">match</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">true</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Haskell classifies <c>--</c> line comments and <c>{- ... -}</c> nested block comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HaskellClassifiesBothCommentForms()
    {
        var html = HaskellLexer.Instance.Render("-- line comment\n{- outer {- inner -} outer -}\nmodule Foo where\nmain :: IO ()\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\">-- line comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cm\">{- outer {- inner -} outer -}</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">module</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">IO</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">where</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Clojure classifies <c>defn</c> declarations, <c>:keyword</c> literals, and the data-bracket forms.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClojureClassifiesDefnAndKeywordLiterals()
    {
        var html = ClojureLexer.Instance.Render("(defn greet [name] {:greeting (str \"hi \" name)})"u8);
        await Assert.That(html.Contains("<span class=\"kd\">defn</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">:greeting</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"p\">[</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"p\">{</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Clojure <c>;</c> line comment classifies as <c>c1</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClojureClassifiesSemicolonComment()
    {
        var html = ClojureLexer.Instance.Render("; this is a comment\n(def x 1)"u8);
        await Assert.That(html.Contains("<span class=\"c1\">; this is a comment</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Scheme classifies <c>define</c> and quote prefixes; doesn't recognize Clojure's <c>[]</c> data brackets.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SchemeClassifiesDefineAndQuote()
    {
        var html = SchemeLexer.Instance.Render("(define (square x) (* x x))\n'(1 2 3)\n`(a ,b)"u8);
        await Assert.That(html.Contains("<span class=\"kd\">define</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">&#39;</span>", StringComparison.Ordinal)
            || html.Contains("<span class=\"o\">'</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the new family aliases to their lexers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesFamilyAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "css"u8], out var css)).IsTrue();
        await Assert.That(css).IsSameReferenceAs(CssLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "scss"u8], out var scss)).IsTrue();
        await Assert.That(scss).IsSameReferenceAs(ScssLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "less"u8], out var less)).IsTrue();
        await Assert.That(less).IsSameReferenceAs(LessLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "ocaml"u8], out var ocaml)).IsTrue();
        await Assert.That(ocaml).IsSameReferenceAs(OcamlLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "haskell"u8], out var haskell)).IsTrue();
        await Assert.That(haskell).IsSameReferenceAs(HaskellLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "clojure"u8], out var clojure)).IsTrue();
        await Assert.That(clojure).IsSameReferenceAs(ClojureLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "scheme"u8], out var scheme)).IsTrue();
        await Assert.That(scheme).IsSameReferenceAs(SchemeLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "ksh"u8], out var ksh)).IsTrue();
        await Assert.That(ksh).IsSameReferenceAs(BashLexer.Instance);
    }
}
