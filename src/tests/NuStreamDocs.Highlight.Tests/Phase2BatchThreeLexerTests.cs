// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the third Phase-2 batch (VB.NET, GraphQL, Protobuf, HCL, R, Julia, MATLAB, Nim).</summary>
public class Phase2BatchThreeLexerTests
{
    /// <summary>VB.NET classifies <c>Sub</c>/<c>Function</c> declarations and the case-insensitive control-flow keywords.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task VbNetClassifiesDeclarationsCaseInsensitively()
    {
        var html = VbNetLexer.Instance.Render("Public Class Foo\n  Public Sub Greet()\n    If x Then Return\n  End Sub\nEnd Class"u8);
        await Assert.That(html.Contains("<span class=\"kd\">Public</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">Class</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">Sub</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">If</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">Return</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">End</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>VB.NET <c>'</c> single-quote line comments classify as <c>c1</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task VbNetClassifiesSingleQuoteComment()
    {
        var html = VbNetLexer.Instance.Render("' inline comment\nDim x = 1\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\">' inline comment</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>GraphQL classifies <c>type</c>/<c>scalar</c>/<c>enum</c> and the built-in scalar types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GraphQLClassifiesSchemaDeclarations()
    {
        var html = GraphQLLexer.Instance.Render("type User {\n  id: ID!\n  name: String\n  age: Int\n}\nenum Role { ADMIN USER }\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">type</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">enum</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">ID</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">String</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">Int</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>GraphQL <c>$variable</c> references classify as Name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GraphQLClassifiesVariableSigil()
    {
        var html = GraphQLLexer.Instance.Render("query GetUser($id: ID!) { user(id: $id) { name } }"u8);
        await Assert.That(html.Contains("<span class=\"k\">query</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$id</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Protobuf classifies <c>message</c>/<c>service</c>/<c>rpc</c> and primitive scalar types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ProtobufClassifiesMessageAndScalars()
    {
        var html = ProtobufLexer.Instance.Render("syntax = \"proto3\";\nmessage User {\n  string name = 1;\n  int32 age = 2;\n  bool active = 3;\n}\n"u8);
        await Assert.That(html.Contains("<span class=\"k\">syntax</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">message</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">string</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">int32</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">bool</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>HCL classifies <c>resource</c>/<c>variable</c>/<c>module</c> declarations and <c>#</c>/<c>//</c> comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HclClassifiesResourceBlocks()
    {
        var html = HclLexer.Instance.Render("# top comment\nresource \"aws_instance\" \"web\" {\n  ami = \"ami-123\"\n  instance_type = \"t2.micro\"\n}\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\"># top comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">resource</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>R classifies <c>function</c>/<c>if</c>/<c>else</c> and the <c>&lt;-</c> assignment operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RClassifiesFunctionAndArrowOperator()
    {
        var html = RLexer.Instance.Render("greet <- function(name) {\n  if (is.null(name)) return(NULL)\n  cat(\"hi\", name)\n}\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">function</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">if</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">return</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">&lt;-</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">NULL</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Julia classifies <c>function</c>/<c>end</c> and <c>#=</c>/<c>=#</c> block comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JuliaClassifiesFunctionAndBlockComment()
    {
        var html = JuliaLexer.Instance.Render("#= block\ncomment =#\nfunction greet(name)\n  return \"hi $name\"\nend\n"u8);
        await Assert.That(html.Contains("<span class=\"cm\">#= block\ncomment =#</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">function</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">return</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">end</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>MATLAB classifies <c>%</c> line comments and <c>%{</c>/<c>%}</c> block comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MatlabClassifiesPercentCommentsAndFunction()
    {
        var html = MatlabLexer.Instance.Render("%{ block comment %}\n% line comment\nfunction y = square(x)\n  y = x^2;\nend\n"u8);
        await Assert.That(html.Contains("<span class=\"cm\">%{ block comment %}</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">% line comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">function</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">end</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Nim classifies <c>proc</c>/<c>let</c>/<c>var</c> declarations and <c>#[</c>/<c>]#</c> block comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NimClassifiesProcAndBlockComment()
    {
        var html = NimLexer.Instance.Render("#[ block comment ]#\n# line comment\nproc greet(name: string): string =\n  return \"hi \" &amp; name\n"u8);
        await Assert.That(html.Contains("<span class=\"cm\">#[ block comment ]#</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\"># line comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">proc</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">string</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">return</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the new aliases to their lexers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesNewAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "vbnet"u8], out var vb)).IsTrue();
        await Assert.That(vb).IsSameReferenceAs(VbNetLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "graphql"u8], out var gql)).IsTrue();
        await Assert.That(gql).IsSameReferenceAs(GraphQLLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "proto"u8], out var proto)).IsTrue();
        await Assert.That(proto).IsSameReferenceAs(ProtobufLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "terraform"u8], out var tf)).IsTrue();
        await Assert.That(tf).IsSameReferenceAs(HclLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "r"u8], out var r)).IsTrue();
        await Assert.That(r).IsSameReferenceAs(RLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "julia"u8], out var jl)).IsTrue();
        await Assert.That(jl).IsSameReferenceAs(JuliaLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "octave"u8], out var oct)).IsTrue();
        await Assert.That(oct).IsSameReferenceAs(MatlabLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "nim"u8], out var nim)).IsTrue();
        await Assert.That(nim).IsSameReferenceAs(NimLexer.Instance);
    }
}
