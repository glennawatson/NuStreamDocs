// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.CFamily;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Tests covering the C# property-accessor state-machine — block-body and arrow-body forms, brace nesting, and pop semantics.</summary>
public class CSharpAccessorStateTests
{
    /// <summary>Block-body accessor: <c>field</c> classified as keyword.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task BlockAccessorBody_classifies_field_as_keyword()
    {
        var html = CSharpLexer.Instance.Render("public int X { get { return field; } }"u8);
        await Assert.That(html.Contains("<span class=\"k\">field</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Block-body accessor: <c>value</c> classified as keyword inside setter.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task BlockSetter_classifies_value_as_keyword()
    {
        var html = CSharpLexer.Instance.Render("public int X { set { field = value; } }"u8);
        await Assert.That(html.Contains("<span class=\"k\">value</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Arrow-body accessor: <c>field</c> classified as keyword and <c>;</c> pops back to root.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ArrowAccessorBody_classifies_field_as_keyword_and_pops_on_semicolon()
    {
        var html = CSharpLexer.Instance.Render("public int X { get => field; } void M() { var field = 1; }"u8);

        // First `field` (in accessor) should be a keyword; second (in method body) should NOT be.
        var firstKeyword = html.IndexOf("<span class=\"k\">field</span>", StringComparison.Ordinal);
        var firstIdentifier = html.IndexOf("<span class=\"n\">field</span>", StringComparison.Ordinal);
        await Assert.That(firstKeyword).IsGreaterThanOrEqualTo(0);
        await Assert.That(firstIdentifier).IsGreaterThan(firstKeyword);
    }

    /// <summary>Nested block inside an accessor (e.g. local function or lambda body) — accessor state survives the inner <c>{...}</c>.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task NestedBlockInsideAccessor_does_not_pop_state_too_early()
    {
        var html = CSharpLexer.Instance.Render("public int X { set { Action a = () => { Console.WriteLine(1); }; field = value; } }"u8);

        // Both `field` and `value` after the lambda's closing `}` must still be classified as keywords.
        await Assert.That(html.Contains("<span class=\"k\">field</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">value</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Multiple accessor blocks in a single property — each one independently pushes and pops.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task MultipleAccessors_each_push_and_pop()
    {
        var html = CSharpLexer.Instance.Render("public int X { get { return field; } set { field = value; } }"u8);

        // `field` should appear as a keyword at least twice (in both getter and setter).
        var firstField = html.IndexOf("<span class=\"k\">field</span>", StringComparison.Ordinal);
        var secondField = html.IndexOf("<span class=\"k\">field</span>", firstField + 1, StringComparison.Ordinal);
        await Assert.That(firstField).IsGreaterThanOrEqualTo(0);
        await Assert.That(secondField).IsGreaterThan(firstField);
    }

    /// <summary>Auto-property <c>{ get; set; }</c> — neither accessor body is entered (no <c>{</c> after the keyword), and `field` later in the file isn't wrongly highlighted.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task AutoProperty_does_not_enter_accessor_state()
    {
        var html = CSharpLexer.Instance.Render("public int X { get; set; } void M() { var field = 1; }"u8);

        // `field` in the method body should be a plain identifier, not a keyword.
        await Assert.That(html.Contains("<span class=\"k\">field</span>", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("<span class=\"n\">field</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>The <c>get</c> / <c>set</c> / <c>init</c> keywords themselves classify as declaration keywords on entry.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task AccessorOpeners_classify_as_declaration_keyword()
    {
        var html = CSharpLexer.Instance.Render("public int X { get => 1; init => field = value; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">get</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">init</span>", StringComparison.Ordinal)).IsTrue();
    }
}
