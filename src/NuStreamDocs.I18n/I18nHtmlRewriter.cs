// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.I18n;

/// <summary>Rewrites <c>&lt;html lang="..."&gt;</c>, injects <c>hreflang</c> alternate links, and emits the <c>.nd-lang-switcher</c> bar.</summary>
internal static class I18nHtmlRewriter
{
    /// <summary>Minimum number of locales required before rendering a language switcher bar.</summary>
    private const int MinSwitcherLocales = 2;

    /// <summary>Gets the opening <c>&lt;html</c> tag prefix bytes.</summary>
    private static ReadOnlySpan<byte> HtmlTagOpen => "<html"u8;

    /// <summary>Gets the closing <c>&lt;/head&gt;</c> tag bytes.</summary>
    private static ReadOnlySpan<byte> HeadClose => "</head>"u8;

    /// <summary>Gets the closing <c>&lt;/header&gt;</c> tag bytes.</summary>
    private static ReadOnlySpan<byte> HeaderClose => "</header>"u8;

    /// <summary>Gets the opening <c>&lt;body</c> tag prefix bytes.</summary>
    private static ReadOnlySpan<byte> BodyOpen => "<body"u8;

    /// <summary>Rewrites <paramref name="html"/> for <paramref name="currentLocale"/> and its sibling <paramref name="alternates"/>.</summary>
    /// <param name="html">Input UTF-8 HTML.</param>
    /// <param name="currentLocale">Active page locale code.</param>
    /// <param name="defaultLocale">Configured site default locale code.</param>
    /// <param name="alternates">Ordered alternate links for this logical page.</param>
    /// <param name="options">Active plugin options.</param>
    /// <param name="output">Destination sink.</param>
    internal static void Rewrite(
        ReadOnlySpan<byte> html,
        string currentLocale,
        string defaultLocale,
        LocaleAlternateLink[] alternates,
        in I18nOptions options,
        IBufferWriter<byte> output)
    {
        var localeBytes = Encoding.UTF8.GetBytes(currentLocale);
        var headIdx = html.IndexOf(HeadClose);
        if (headIdx < 0)
        {
            WriteWithHtmlLang(html, localeBytes, output);
            return;
        }

        WriteWithHtmlLang(html[..headIdx], localeBytes, output);
        if (options.EmitHreflangTags && alternates.Length > 0)
        {
            WriteHreflangLinks(output, alternates, defaultLocale);
        }

        var fromHeadClose = html[headIdx..];
        if (!options.EmitLanguageSwitcher || alternates.Length < MinSwitcherLocales)
        {
            output.Write(fromHeadClose);
            return;
        }

        WriteBodyWithSwitcher(fromHeadClose, currentLocale, alternates, output);
    }

    /// <summary>Rewrites or injects <c>lang="{locale}"</c> on the root <c>&lt;html&gt;</c> tag within <paramref name="prefix"/>.</summary>
    /// <param name="prefix">HTML slice starting from document start.</param>
    /// <param name="localeBytes">UTF-8 BCP-47 locale bytes.</param>
    /// <param name="output">Destination buffer.</param>
    private static void WriteWithHtmlLang(
        ReadOnlySpan<byte> prefix,
        ReadOnlySpan<byte> localeBytes,
        IBufferWriter<byte> output)
    {
        var htmlPos = prefix.IndexOf(HtmlTagOpen);
        if (htmlPos < 0)
        {
            output.Write(prefix);
            return;
        }

        var tagCloseRel = prefix[htmlPos..].IndexOf((byte)'>');
        if (tagCloseRel < 0)
        {
            output.Write(prefix);
            return;
        }

        output.Write(prefix[..htmlPos]);
        var htmlTag = prefix.Slice(htmlPos, tagCloseRel + 1);
        RewriteSingleHtmlOpenTag(htmlTag, localeBytes, output);
        output.Write(prefix[(htmlPos + tagCloseRel + 1)..]);
    }

    /// <summary>Writes <paramref name="htmlTag"/> with its <c>lang</c> attribute set to <paramref name="localeBytes"/>.</summary>
    /// <param name="htmlTag">Complete <c>&lt;html ...&gt;</c> slice.</param>
    /// <param name="localeBytes">UTF-8 locale tag.</param>
    /// <param name="output">Destination sink.</param>
    private static void RewriteSingleHtmlOpenTag(
        ReadOnlySpan<byte> htmlTag,
        ReadOnlySpan<byte> localeBytes,
        IBufferWriter<byte> output)
    {
        var langAttr = " lang=\""u8;
        var existingIdx = htmlTag.IndexOf(langAttr);
        if (existingIdx >= 0)
        {
            var valStart = existingIdx + langAttr.Length;
            var endQuoteRel = htmlTag[valStart..].IndexOf((byte)'"');
            if (endQuoteRel >= 0)
            {
                output.Write(htmlTag[..valStart]);
                XmlEntityEscaper.WriteEscaped(output, localeBytes, XmlEntityEscaper.Mode.HtmlAttribute);
                output.Write(htmlTag[(valStart + endQuoteRel)..]);
                return;
            }
        }

        output.Write(htmlTag[..^1]);
        output.Write(langAttr);
        XmlEntityEscaper.WriteEscaped(output, localeBytes, XmlEntityEscaper.Mode.HtmlAttribute);
        output.Write("\">"u8);
    }

    /// <summary>Writes <c>&lt;link rel="alternate" hreflang="..." href="..."&gt;</c> tags plus <c>x-default</c>.</summary>
    /// <param name="output">Destination buffer.</param>
    /// <param name="alternates">Locale links.</param>
    /// <param name="defaultLocale">Default locale code.</param>
    private static void WriteHreflangLinks(
        IBufferWriter<byte> output,
        LocaleAlternateLink[] alternates,
        string defaultLocale)
    {
        byte[]? defaultHref = null;
        for (var i = 0; i < alternates.Length; i++)
        {
            var alt = alternates[i];
            WriteSingleHreflang(output, Encoding.UTF8.GetBytes(alt.LocaleCode), alt.Href);
            if (string.Equals(alt.LocaleCode, defaultLocale, StringComparison.OrdinalIgnoreCase))
            {
                defaultHref = alt.Href;
            }
        }

        if (defaultHref is not null)
        {
            WriteSingleHreflang(output, "x-default"u8, defaultHref);
        }
    }

    /// <summary>Writes one <c>hreflang</c> link element.</summary>
    /// <param name="output">Destination sink.</param>
    /// <param name="hreflang">BCP-47 tag or <c>x-default</c>.</param>
    /// <param name="href">Target URL bytes.</param>
    private static void WriteSingleHreflang(
        IBufferWriter<byte> output,
        ReadOnlySpan<byte> hreflang,
        ReadOnlySpan<byte> href)
    {
        output.Write("<link rel=\"alternate\" hreflang=\""u8);
        XmlEntityEscaper.WriteEscaped(output, hreflang, XmlEntityEscaper.Mode.HtmlAttribute);
        output.Write("\" href=\""u8);
        XmlEntityEscaper.WriteEscaped(output, href, XmlEntityEscaper.Mode.HtmlAttribute);
        output.Write("\">\n"u8);
    }

    /// <summary>Splices the <c>.nd-lang-switcher</c> bar right before <c>&lt;/header&gt;</c> (or immediately inside <c>&lt;body&gt;</c> when no header exists).</summary>
    /// <param name="fromHeadClose">HTML slice starting at <c>&lt;/head&gt;</c>.</param>
    /// <param name="currentLocale">Current page locale code.</param>
    /// <param name="alternates">Locale links.</param>
    /// <param name="output">Destination buffer.</param>
    private static void WriteBodyWithSwitcher(
        ReadOnlySpan<byte> fromHeadClose,
        string currentLocale,
        LocaleAlternateLink[] alternates,
        IBufferWriter<byte> output)
    {
        var headerClose = fromHeadClose.IndexOf(HeaderClose);
        if (headerClose >= 0)
        {
            output.Write(fromHeadClose[..headerClose]);
            WriteSwitcherMarkup(output, currentLocale, alternates);
            output.Write(fromHeadClose[headerClose..]);
            return;
        }

        var bodyPos = fromHeadClose.IndexOf(BodyOpen);
        if (bodyPos >= 0)
        {
            var bodyCloseRel = fromHeadClose[bodyPos..].IndexOf((byte)'>');
            if (bodyCloseRel >= 0)
            {
                var split = bodyPos + bodyCloseRel + 1;
                output.Write(fromHeadClose[..split]);
                WriteSwitcherMarkup(output, currentLocale, alternates);
                output.Write(fromHeadClose[split..]);
                return;
            }
        }

        output.Write(fromHeadClose);
    }

    /// <summary>Writes the <c>&lt;nav class="nd-lang-switcher"&gt;</c> HTML fragment.</summary>
    /// <param name="output">Destination sink.</param>
    /// <param name="currentLocale">Current locale code.</param>
    /// <param name="alternates">Available locales for this page.</param>
    private static void WriteSwitcherMarkup(
        IBufferWriter<byte> output,
        string currentLocale,
        LocaleAlternateLink[] alternates)
    {
        output.Write("<nav class=\"nd-lang-switcher\" aria-label=\"Language\">"u8);
        for (var i = 0; i < alternates.Length; i++)
        {
            var alt = alternates[i];
            var isCurrent = string.Equals(alt.LocaleCode, currentLocale, StringComparison.OrdinalIgnoreCase);
            output.Write("<a href=\""u8);
            XmlEntityEscaper.WriteEscaped(output, alt.Href, XmlEntityEscaper.Mode.HtmlAttribute);
            output.Write("\" lang=\""u8);
            XmlEntityEscaper.WriteEscaped(output, Encoding.UTF8.GetBytes(alt.LocaleCode), XmlEntityEscaper.Mode.HtmlAttribute);
            if (isCurrent)
            {
                output.Write("\" aria-current=\"page"u8);
            }

            output.Write("\">"u8);
            XmlEntityEscaper.WriteEscaped(output, alt.DisplayName, XmlEntityEscaper.Mode.Xml);
            output.Write("</a>"u8);
        }

        output.Write("</nav>"u8);
    }
}
