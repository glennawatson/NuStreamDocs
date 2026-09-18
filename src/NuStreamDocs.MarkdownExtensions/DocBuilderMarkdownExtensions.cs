// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.MarkdownExtensions.Abbr;
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
using NuStreamDocs.MarkdownExtensions.Snippets;
using NuStreamDocs.MarkdownExtensions.Tables;
using NuStreamDocs.MarkdownExtensions.Tabs;

namespace NuStreamDocs.MarkdownExtensions;

/// <summary>
/// Builder-extension surface for the common Markdown extensions.
/// Each method registers one preprocessor plugin that works under
/// either <c>NuStreamDocs.Theme.Material</c> or
/// <c>NuStreamDocs.Theme.Material3</c>.
/// </summary>
public static class DocBuilderMarkdownExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">The builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="AdmonitionPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAdmonitions() => builder.UsePlugin(new AdmonitionPlugin());

        /// <summary>Registers <see cref="DetailsPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseDetails() => builder.UsePlugin(new DetailsPlugin());

        /// <summary>Registers <see cref="TabsPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseTabs() => builder.UsePlugin(new TabsPlugin());

        /// <summary>Registers <see cref="CheckListPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseCheckLists() => builder.UsePlugin(new CheckListPlugin());

        /// <summary>Registers <see cref="MarkPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMark() => builder.UsePlugin(new MarkPlugin());

        /// <summary>Registers <see cref="CaretTildePlugin"/> for sup/sub/ins/del rendering.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseCaretTilde() => builder.UsePlugin(new CaretTildePlugin());

        /// <summary>Registers <see cref="CriticMarkupPlugin"/> for CriticMarkup span rewriting.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseCriticMarkup() => builder.UsePlugin(new CriticMarkupPlugin());

        /// <summary>Registers <see cref="InlineHilitePlugin"/> for <c>`#!lang code`</c> inline highlight rewriting.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseInlineHilite() => builder.UsePlugin(new InlineHilitePlugin());

        /// <summary>Registers <see cref="MdInHtmlPlugin"/> for parsing Markdown inside <c>markdown="1"</c> HTML blocks.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMarkdownInHtml() => builder.UsePlugin(new MdInHtmlPlugin());

        /// <summary>Registers <see cref="DefListPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseDefinitionLists() => builder.UsePlugin(new DefListPlugin());

        /// <summary>Registers <see cref="FootnotesPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseFootnotes() => builder.UsePlugin(new FootnotesPlugin());

        /// <summary>Registers <see cref="TablesPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseTables() => builder.UsePlugin(new TablesPlugin());

        /// <summary>Registers <see cref="AttrListPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAttrList() => builder.UsePlugin(new AttrListPlugin());

        /// <summary>Registers <see cref="AbbrPlugin"/> for <c>*[ABBR]: definition</c> expansion into <c>&lt;abbr&gt;</c> elements.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAbbreviations() => builder.UsePlugin(new AbbrPlugin());

        /// <summary>Registers <see cref="SnippetsPlugin"/> for <c>--8&lt;-- "path"</c> file inclusions.</summary>
        /// <param name="basePaths">Directories to resolve snippet paths against, in priority order.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSnippets(params DirectoryPath[] basePaths) =>
            builder.UsePlugin(new SnippetsPlugin(basePaths));

        /// <summary>Registers every common Markdown extension in one call (admonitions, details, tabs, etc.).</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseCommonMarkdownExtensions() =>
            builder
                .UseAdmonitions()
                .UseDetails()
                .UseTabs()
                .UseCheckLists()
                .UseMark()
                .UseCaretTilde()
                .UseCriticMarkup()
                .UseMarkdownInHtml()
                .UseDefinitionLists()
                .UseFootnotes()
                .UseTables()
                .UseAttrList()
                .UseAbbreviations();
    }
}
