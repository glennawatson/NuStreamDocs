// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the RST / HTTP / GLSL / HLSL lexers.</summary>
public class RstHttpGlslLexerTests
{
    /// <summary>RST classifies <c>..</c> directives at line start.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RstClassifiesDirectives()
    {
        var html = RstLexer.Instance.Render(".. code-block:: python\n\n   print('hi')\n"u8);
        await Assert.That(html.Contains("<span class=\"cp\">.. code-block:: python</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>RST classifies inline literals (<c>``code``</c>) and emphasis runs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RstClassifiesInlineMarkup()
    {
        var html = RstLexer.Instance.Render("Use ``code`` and *italic* and **bold** here."u8);
        await Assert.That(html.Contains("<span class=\"s1\">``code``</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">*italic*</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">**bold**</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>RST classifies heading underlines as a marker line.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RstClassifiesHeadingUnderlines()
    {
        var html = RstLexer.Instance.Render("Title\n=====\n\nSubtitle\n--------\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">=====</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">--------</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>RST classifies field-list labels (<c>:field:</c>) and substitution references (<c>|name|</c>).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RstClassifiesFieldsAndSubstitutions()
    {
        var html = RstLexer.Instance.Render(":Author: Alice\n\nThis is |version| of the docs."u8);
        await Assert.That(html.Contains("<span class=\"na\">:Author:</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">|version|</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>RST classifies bullet markers at line start.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RstClassifiesBulletMarkers()
    {
        var html = RstLexer.Instance.Render("- first item\n- second item\n"u8);
        await Assert.That(html.Contains("<span class=\"o\">- </span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>HTTP classifies request lines as a single keyword token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HttpClassifiesRequestLine()
    {
        var html = HttpLexer.Instance.Render("GET /api/users HTTP/1.1\nHost: example.com\n"u8);
        await Assert.That(html.Contains("<span class=\"k\">GET /api/users HTTP/1.1</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">Host</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>HTTP classifies response status lines as a single keyword token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HttpClassifiesStatusLine()
    {
        var html = HttpLexer.Instance.Render("HTTP/1.1 200 OK\nContent-Type: application/json\n"u8);
        await Assert.That(html.Contains("<span class=\"k\">HTTP/1.1 200 OK</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">Content-Type</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>GLSL classifies vector / matrix / sampler types and storage qualifiers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GlslClassifiesShaderTypes()
    {
        var html = GlslLexer.Instance.Render("#version 330 core\nuniform sampler2D tex;\nin vec3 normal;\nout vec4 fragColor;\nvoid main() { fragColor = texture(tex, vec2(0.0, 0.0)); }"u8);
        await Assert.That(html.Contains("<span class=\"cp\">", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">uniform</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">sampler2D</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">vec3</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">vec4</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">void</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>GLSL classifies the <c>discard</c> keyword.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GlslClassifiesDiscard()
    {
        var html = GlslLexer.Instance.Render("if (alpha < 0.1) { discard; }"u8);
        await Assert.That(html.Contains("<span class=\"k\">discard</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>HLSL classifies dimensioned float / matrix types and <c>cbuffer</c> declarations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HlslClassifiesShaderTypes()
    {
        var html = HlslLexer.Instance.Render("cbuffer Constants : register(b0) { float4x4 World; float4 Tint; }\nTexture2D<float4> tex : register(t0);"u8);
        await Assert.That(html.Contains("<span class=\"kd\">cbuffer</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">float4x4</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">float4</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">Texture2D</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the new aliases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesNewAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "rst"u8], out var rst)).IsTrue();
        await Assert.That(rst).IsSameReferenceAs(RstLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "http"u8], out var http)).IsTrue();
        await Assert.That(http).IsSameReferenceAs(HttpLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "glsl"u8], out var glsl)).IsTrue();
        await Assert.That(glsl).IsSameReferenceAs(GlslLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "hlsl"u8], out var hlsl)).IsTrue();
        await Assert.That(hlsl).IsSameReferenceAs(HlslLexer.Instance);
    }
}
