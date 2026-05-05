// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Templating.Tests;

/// <summary>End-to-end tests for the UTF-8 Mustache-style <c>Template</c>.</summary>
public class TemplateTests
{
    /// <summary>A literal-only template should round-trip its bytes verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersLiteralVerbatim()
    {
        var html = Render("hello world"u8, TemplateData.Empty);
        await Assert.That(html).IsEqualTo("hello world");
    }

    /// <summary>Escaped variable should HTML-escape its value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EscapedVariableEscapesHtml()
    {
        var data = Build([("name", "a < b & c")], []);
        var html = Render("hi {{name}}"u8, data);
        await Assert.That(html).IsEqualTo("hi a &lt; b &amp; c");
    }

    /// <summary>Triple-mustache should write the value verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TripleVariableWritesRaw()
    {
        var data = Build([("html", "<i>x</i>")], []);
        var html = Render("{{{html}}}"u8, data);
        await Assert.That(html).IsEqualTo("<i>x</i>");
    }

    /// <summary>A truthy section should render its body once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TruthySectionRendersBody()
    {
        var data = Build([("flag", "yes"), ("inner", "ok")], []);
        var html = Render("{{#flag}}[{{inner}}]{{/flag}}"u8, data);
        await Assert.That(html).IsEqualTo("[ok]");
    }

    /// <summary>An inverted section should render only when the key is falsy.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvertedSectionRendersWhenFalsy()
    {
        var data = Build([("inner", "fallback")], []);
        var html = Render("{{^missing}}[{{inner}}]{{/missing}}"u8, data);
        await Assert.That(html).IsEqualTo("[fallback]");
    }

    /// <summary>A section over an array of items should render the body once per item.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SectionIteratesEveryItem()
    {
        var first = Build([("title", "A")], []);
        var second = Build([("title", "B")], []);
        var third = Build([("title", "C")], []);
        var data = Build([], [("nav", [first, second, third])]);
        var html = Render("<{{#nav}}{{title}}|{{/nav}}>"u8, data);
        await Assert.That(html).IsEqualTo("<A|B|C|>");
    }

    /// <summary>Section iteration should expose the outer scope's scalars too.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SectionInheritsOuterScopeScalars()
    {
        var first = Build([("title", "A")], []);
        var second = Build([("title", "B")], []);
        var data = Build([("brand", "Site")], [("nav", [first, second])]);
        var html = Render("{{#nav}}[{{brand}}/{{title}}]{{/nav}}"u8, data);
        await Assert.That(html).IsEqualTo("[Site/A][Site/B]");
    }

    /// <summary>Partial inclusions should render the matching template under the current scope.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PartialIsIncludedAndRendered()
    {
        var partial = Template.Compile("{{name}}!"u8);
        Dictionary<string, Template> partials = new(StringComparer.Ordinal)
        {
            ["greeting"] = partial
        };
        var data = Build([("name", "world")], []);
        var html = RenderWithPartials("hi {{> greeting}}"u8, data, partials);
        await Assert.That(html).IsEqualTo("hi world!");
    }

    /// <summary>An unknown partial name should render as an empty span and not throw.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnknownPartialRendersEmpty()
    {
        Dictionary<string, Template> partials = new(StringComparer.Ordinal);
        var html = RenderWithPartials("a{{> missing}}b"u8, TemplateData.Empty, partials);
        await Assert.That(html).IsEqualTo("ab");
    }

    /// <summary>Comments should be skipped at compile time.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CommentsAreSkipped()
    {
        var html = Render("a{{! ignore }}b"u8, TemplateData.Empty);
        await Assert.That(html).IsEqualTo("ab");
    }

    /// <summary>An unclosed tag should throw a <c>TemplateSyntaxException</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnterminatedTagThrows()
    {
        var ex = Assert.Throws<TemplateSyntaxException>(static () => Template.Compile("hi {{name"u8));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Mismatched section close should throw.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MismatchedSectionCloseThrows()
    {
        var ex = Assert.Throws<TemplateSyntaxException>(static () =>
            Template.Compile("{{#a}}body{{/b}}"u8));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Convenience: compile + render with a partial map + decode.</summary>
    /// <param name="source">UTF-8 template source.</param>
    /// <param name="data">Root data scope.</param>
    /// <param name="partials">Compiled partial registry.</param>
    /// <returns>The rendered string.</returns>
    private static string RenderWithPartials(
        ReadOnlySpan<byte> source,
        TemplateData data,
        Dictionary<string, Template> partials)
    {
        var template = Template.Compile(source);
        ArrayBufferWriter<byte> writer = new();
        Dictionary<byte[], Template> bytePartials = new(partials.Count, Common.ByteArrayComparer.Instance);
        foreach (var pair in partials)
        {
            bytePartials[Encoding.UTF8.GetBytes(pair.Key)] = pair.Value;
        }

        template.Render(data, bytePartials, writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    /// <summary>Convenience: compile + render + decode.</summary>
    /// <param name="source">UTF-8 template source.</param>
    /// <param name="data">Root data scope.</param>
    /// <returns>The rendered string.</returns>
    private static string Render(ReadOnlySpan<byte> source, TemplateData data)
    {
        var template = Template.Compile(source);
        ArrayBufferWriter<byte> writer = new();
        template.Render(data, writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    /// <summary>Convenience: builds a <c>TemplateData</c> from string literals.</summary>
    /// <param name="scalars">Scalar key/value pairs.</param>
    /// <param name="sections">Section key/value pairs.</param>
    /// <returns>The built data scope.</returns>
    private static TemplateData Build(
        (string Key, string Value)[] scalars,
        (string Key, TemplateData[] Items)[] sections)
    {
        Dictionary<byte[], ReadOnlyMemory<byte>> s = new(scalars.Length, Common.ByteArrayComparer.Instance);
        for (var i = 0; i < scalars.Length; i++)
        {
            s[Encoding.UTF8.GetBytes(scalars[i].Key)] = Encoding.UTF8.GetBytes(scalars[i].Value);
        }

        Dictionary<byte[], TemplateData[]> t = new(sections.Length, Common.ByteArrayComparer.Instance);
        for (var i = 0; i < sections.Length; i++)
        {
            t[Encoding.UTF8.GetBytes(sections[i].Key)] = sections[i].Items;
        }

        return new(s, t);
    }
}
