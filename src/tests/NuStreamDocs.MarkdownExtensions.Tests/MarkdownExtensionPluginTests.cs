// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Building;
using NuStreamDocs.MarkdownExtensions.Admonitions;
using NuStreamDocs.MarkdownExtensions.AttrList;
using NuStreamDocs.MarkdownExtensions.CaretTilde;
using NuStreamDocs.MarkdownExtensions.CheckList;
using NuStreamDocs.MarkdownExtensions.CriticMarkup;
using NuStreamDocs.MarkdownExtensions.DefList;
using NuStreamDocs.MarkdownExtensions.Details;
using NuStreamDocs.MarkdownExtensions.Footnotes;
using NuStreamDocs.MarkdownExtensions.InlineHilite;
using NuStreamDocs.MarkdownExtensions.Mark;
using NuStreamDocs.MarkdownExtensions.MdInHtml;
using NuStreamDocs.MarkdownExtensions.Tables;
using NuStreamDocs.MarkdownExtensions.Tabs;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Lifecycle / registration tests for every common Markdown-extension plugin.</summary>
public class MarkdownExtensionPluginTests
{
    /// <summary>Each plugin advertises a stable name.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PluginNamesAreStable()
    {
        await Assert.That(new AdmonitionPlugin().Name.SequenceEqual("admonitions"u8)).IsTrue();
        await Assert.That(new AttrListPlugin().Name.SequenceEqual("attr-list"u8)).IsTrue();
        await Assert.That(new CaretTildePlugin().Name.SequenceEqual("caret-tilde"u8)).IsTrue();
        await Assert.That(new CheckListPlugin().Name.SequenceEqual("checklist"u8)).IsTrue();
        await Assert.That(new CriticMarkupPlugin().Name.SequenceEqual("critic"u8)).IsTrue();
        await Assert.That(new DefListPlugin().Name.SequenceEqual("deflist"u8)).IsTrue();
        await Assert.That(new DetailsPlugin().Name.SequenceEqual("details"u8)).IsTrue();
        await Assert.That(new FootnotesPlugin().Name.SequenceEqual("footnotes"u8)).IsTrue();
        await Assert.That(new InlineHilitePlugin().Name.SequenceEqual("inlinehilite"u8)).IsTrue();
        await Assert.That(new MarkPlugin().Name.SequenceEqual("mark"u8)).IsTrue();
        await Assert.That(new MdInHtmlPlugin().Name.SequenceEqual("md_in_html"u8)).IsTrue();
        await Assert.That(new TablesPlugin().Name.SequenceEqual("tables"u8)).IsTrue();
        await Assert.That(new TabsPlugin().Name.SequenceEqual("tabs"u8)).IsTrue();
    }

    /// <summary>Every preprocessor passes its source through to the rewriter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessorsRunWithoutThrowing()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        new AdmonitionPlugin().Preprocess("plain"u8, sink);
        new CaretTildePlugin().Preprocess("plain"u8, sink);
        new CheckListPlugin().Preprocess("plain"u8, sink);
        new CriticMarkupPlugin().Preprocess("plain"u8, sink);
        new DefListPlugin().Preprocess("plain"u8, sink);
        new DetailsPlugin().Preprocess("plain"u8, sink);
        new FootnotesPlugin().Preprocess("plain"u8, sink);
        new InlineHilitePlugin().Preprocess("plain"u8, sink);
        new MarkPlugin().Preprocess("plain"u8, sink);
        new MdInHtmlPlugin().Preprocess("plain"u8, sink);
        new TablesPlugin().Preprocess("plain"u8, sink);
        new TabsPlugin().Preprocess("plain"u8, sink);
        await Assert.That(sink.WrittenCount).IsGreaterThan(0);
    }

    /// <summary>Every <c>Use*()</c> builder extension registers and chains.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseEachRegisters()
    {
        var builder = new DocBuilder()
            .UseAdmonitions()
            .UseDetails()
            .UseTabs()
            .UseCheckLists()
            .UseMark()
            .UseCaretTilde()
            .UseCriticMarkup()
            .UseInlineHilite()
            .UseMarkdownInHtml()
            .UseDefinitionLists()
            .UseFootnotes()
            .UseTables()
            .UseAttrList();
        await Assert.That(builder).IsTypeOf<DocBuilder>();
    }

    /// <summary>UseCommonMarkdownExtensions chains every plugin in one call.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseCommonRegisters()
    {
        var builder = new DocBuilder().UseCommonMarkdownExtensions();
        await Assert.That(builder).IsTypeOf<DocBuilder>();
    }

    /// <summary>Each <c>Use*()</c> rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseEachRejectsNullBuilder()
    {
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseAdmonitions(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseDetails(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseTabs(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseCheckLists(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseMark(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseCaretTilde(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseCriticMarkup(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseInlineHilite(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseMarkdownInHtml(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseDefinitionLists(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseFootnotes(null!));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseTables(null!));
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderMarkdownExtensions.UseAttrList(null!));
        await Assert.That(ex).IsNotNull();
    }
}
