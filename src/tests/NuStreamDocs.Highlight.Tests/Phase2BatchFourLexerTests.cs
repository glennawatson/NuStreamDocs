// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Highlight.Languages.CFamily;
using NuStreamDocs.Highlight.Languages.Schema;
using NuStreamDocs.Highlight.Languages.Scripting;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the fourth Phase-2 batch (Crystal, V, Erlang, Elixir).</summary>
public class Phase2BatchFourLexerTests
{
    /// <summary>Crystal classifies <c>def</c>/<c>class</c>/<c>module</c> declarations and <c>#</c> line comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CrystalClassifiesDeclarationsAndComments()
    {
        var html = CrystalLexer.Instance.Render("# top comment\nclass Foo\n  def greet(name : String) : String\n    \"hi #{name}\"\n  end\nend"u8);
        await Assert.That(html.Contains("<span class=\"c1\"># top comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">def</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">String</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">end</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>V classifies <c>fn</c>/<c>struct</c>/<c>pub</c>/<c>mut</c> and primitive types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task VClassifiesFnAndModifiers()
    {
        var html = VLexer.Instance.Render("module main\npub fn greet(mut name string) string {\n  return \"hi \" + name\n}"u8);
        await Assert.That(html.Contains("<span class=\"kd\">module</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">pub</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">fn</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">mut</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">string</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Erlang classifies <c>-module</c>/<c>-export</c> attributes and <c>%</c> line comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ErlangClassifiesModuleAttributesAndComments()
    {
        var html = ErlangLexer.Instance.Render("-module(my_mod).\n-export([greet/1]).\n% the greeter\ngreet(Name) -> io:format(\"hi ~p~n\", [Name])."u8);
        await Assert.That(html.Contains("<span class=\"kd\">-module</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">-export</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">% the greeter</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Erlang classifies uppercase-leading variables as <c>nc</c> and lowercase atoms as <c>n</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ErlangClassifiesVariablesVsAtoms()
    {
        var html = ErlangLexer.Instance.Render("greet(Name) -> Name."u8);
        await Assert.That(html.Contains("<span class=\"nc\">Name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">greet</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Elixir classifies <c>defmodule</c>/<c>def</c> declarations and <c>:atom</c> literals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ElixirClassifiesDefmoduleAndAtoms()
    {
        var html = ElixirLexer.Instance.Render("defmodule Greeter do\n  def hello(:world), do: \"hi\"\nend"u8);
        await Assert.That(html.Contains("<span class=\"kd\">defmodule</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">def</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">do</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">end</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s1\">:world</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Elixir classifies <c>~r/.../</c> sigils and <c>@moduledoc</c> module attributes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ElixirClassifiesSigilsAndModuleAttributes()
    {
        var html = ElixirLexer.Instance.Render("@moduledoc \"\"\nx = ~r/hello/i\ny = ~w(a b c)"u8);
        await Assert.That(html.Contains("<span class=\"na\">@moduledoc</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">~r/hello/i</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">~w(a b c)</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>GraphQL still classifies after the SchemaFamilyRules refactor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "GraphQL is a registered trademark.")]
    public async Task GraphQLAfterSchemaFamilyRefactor()
    {
        var html = GraphQLLexer.Instance.Render("type User { id: ID! }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">type</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">ID</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Protobuf still classifies after the SchemaFamilyRules refactor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ProtobufAfterSchemaFamilyRefactor()
    {
        var html = ProtobufLexer.Instance.Render("message User { string name = 1; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">message</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">string</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>HCL still classifies after the SchemaFamilyRules refactor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HclAfterSchemaFamilyRefactor()
    {
        var html = HclLexer.Instance.Render("resource \"aws_instance\" \"web\" { ami = \"ami-123\" }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">resource</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the new aliases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesNewAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "crystal"u8], out var cr)).IsTrue();
        await Assert.That(cr).IsSameReferenceAs(CrystalLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "v"u8], out var v)).IsTrue();
        await Assert.That(v).IsSameReferenceAs(VLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "erlang"u8], out var erl)).IsTrue();
        await Assert.That(erl).IsSameReferenceAs(ErlangLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "elixir"u8], out var ex)).IsTrue();
        await Assert.That(ex).IsSameReferenceAs(ElixirLexer.Instance);
    }
}
