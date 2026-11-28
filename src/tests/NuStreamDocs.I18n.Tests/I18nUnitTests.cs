// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.I18n.Tests;

/// <summary>Unit tests for <see cref="I18nOptions"/>, <see cref="LocaleRouteClassifier"/>, <see cref="I18nHtmlRewriter"/>, and <see cref="I18nSitemapEnricher"/>.</summary>
public sealed class I18nUnitTests
{
    /// <summary>Primary locale code.</summary>
    private const string EnCode = "en";

    /// <summary>French locale code.</summary>
    private const string FrCode = "fr";

    /// <summary>Expected count of two locales.</summary>
    private const int TwoLocalesCount = 2;

    /// <summary>Gets the shared notice Markdown bytes.</summary>
    private static ReadOnlySpan<byte> NoticeBytes => "> Notice"u8;

    /// <summary>Verifies <see cref="I18nOptions"/> defaults, duplicate-locale replacement, and argument validation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OptionsAndExtensions_HandleAddReplaceDisableAndValidation()
    {
        var updated = I18nOptions.Default
            .AddLocale(EnCode, "English (US)"u8)
            .AddLocale(FrCode, "Français"u8)
            .WithDefaultLocale(FrCode)
            .WithUntranslatedNotice("> Untranslated"u8)
            .WithoutFallback()
            .WithoutHreflang()
            .WithoutLanguageSwitcher()
            .WithoutSitemapEnrichment()
            .Disable();

        await Assert.That(updated.Enabled).IsFalse();
        await Assert.That(updated.DefaultLocale).IsEqualTo(FrCode);
        await Assert.That(updated.Locales.Length).IsEqualTo(TwoLocalesCount);
        await Assert.That(Encoding.UTF8.GetString(updated.Locales[0].DisplayName)).IsEqualTo("English (US)");
        await Assert.That(updated.FallbackToDefaultLocale).IsFalse();
        await Assert.That(updated.EmitHreflangTags).IsFalse();
        await Assert.That(updated.EmitLanguageSwitcher).IsFalse();
        await Assert.That(updated.EnrichSitemap).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(updated.UntranslatedNotice)).IsEqualTo("> Untranslated");

        await Assert.That(static () => I18nOptions.Default.AddLocale(" ", "X"u8)).Throws<ArgumentException>();
        await Assert.That(static () => I18nOptions.Default.WithDefaultLocale(string.Empty)).Throws<ArgumentException>();
    }

    /// <summary>Verifies <see cref="LocaleRouteClassifier"/> across prefixed, suffixed, nested, non-markdown, and root index paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LocaleRouteClassifier_ClassifiesPrefixSuffixAndDefaultPaths()
    {
        HashSet<string> known = [with(StringComparer.OrdinalIgnoreCase), EnCode, FrCode];

        LocaleRouteClassifier.Classify("FR\\guide\\intro.md", known, EnCode, out var loc1, out var path1);
        await Assert.That(loc1).IsEqualTo(FrCode);
        await Assert.That(path1).IsEqualTo("guide/intro.md");

        LocaleRouteClassifier.Classify("guide/intro.fr.md", known, EnCode, out var loc2, out var path2);
        await Assert.That(loc2).IsEqualTo(FrCode);
        await Assert.That(path2).IsEqualTo("guide/intro.md");

        LocaleRouteClassifier.Classify("intro.fr.md", known, EnCode, out var loc3, out var path3);
        await Assert.That(loc3).IsEqualTo(FrCode);
        await Assert.That(path3).IsEqualTo("intro.md");

        LocaleRouteClassifier.Classify("guide/intro.de.md", known, EnCode, out var locUnknownSuffix, out var pathUnknownSuffix);
        await Assert.That(locUnknownSuffix).IsEqualTo(EnCode);
        await Assert.That(pathUnknownSuffix).IsEqualTo("guide/intro.de.md");

        LocaleRouteClassifier.Classify(".md", known, EnCode, out var locDot, out _);
        await Assert.That(locDot).IsEqualTo(EnCode);

        LocaleRouteClassifier.Classify("assets/logo.txt", known, EnCode, out var locTxt, out _);
        await Assert.That(locTxt).IsEqualTo(EnCode);

        LocaleRouteClassifier.Classify(default, known, EnCode, out var locEmpty, out var pathEmpty);
        await Assert.That(locEmpty).IsEqualTo(EnCode);
        await Assert.That(pathEmpty).IsEqualTo(string.Empty);

        var rootHrefWithBase = LocaleRouteClassifier.ComposeHref("index.md", "https://docs.test/"u8, true);
        await Assert.That(Encoding.UTF8.GetString(rootHrefWithBase)).IsEqualTo("https://docs.test/");

        var relHrefNoBase = LocaleRouteClassifier.ComposeHref("en/setup.md", default, false);
        await Assert.That(Encoding.UTF8.GetString(relHrefNoBase)).IsEqualTo("/en/setup.html");
    }

    /// <summary>Verifies <see cref="I18nHtmlRewriter"/> across existing/missing <c>lang</c>, missing <c>&lt;head&gt;</c>, body fallback, and unclosed tags.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task I18nHtmlRewriter_CoversAllDomShapes()
    {
        LocaleAlternateLink[] alts =
        [
            new(EnCode, "English"u8.ToArray(), "/en/"u8.ToArray()),
            new(FrCode, "Français"u8.ToArray(), "/fr/"u8.ToArray()),
        ];

        var noHtmlNoHead = new ArrayBufferWriter<byte>();
        I18nHtmlRewriter.Rewrite("<p>fragment</p>"u8, FrCode, EnCode, alts, I18nOptions.Default, noHtmlNoHead);
        await Assert.That(Encoding.UTF8.GetString(noHtmlNoHead.WrittenSpan)).IsEqualTo("<p>fragment</p>");

        var unclosedHtml = new ArrayBufferWriter<byte>();
        I18nHtmlRewriter.Rewrite("<html attr"u8, FrCode, EnCode, alts, I18nOptions.Default, unclosedHtml);
        await Assert.That(Encoding.UTF8.GetString(unclosedHtml.WrittenSpan)).IsEqualTo("<html attr");

        var htmlWithoutLang = new ArrayBufferWriter<byte>();
        I18nHtmlRewriter.Rewrite("<html><head></head><body><main>Hi</main></body></html>"u8, FrCode, EnCode, alts, I18nOptions.Default, htmlWithoutLang);
        var bodyFallbackResult = Encoding.UTF8.GetString(htmlWithoutLang.WrittenSpan);
        await Assert.That(bodyFallbackResult).Contains("<html lang=\"fr\">");
        await Assert.That(bodyFallbackResult).Contains("<body><nav class=\"nd-lang-switcher\"");
        await Assert.That(bodyFallbackResult).Contains("hreflang=\"x-default\" href=\"/en/\"");

        var unclosedLangQuote = new ArrayBufferWriter<byte>();
        I18nHtmlRewriter.Rewrite("<html lang=en><head></head><body unclosed"u8, FrCode, "es", alts, I18nOptions.Default, unclosedLangQuote);
        var unclosedBodyStr = Encoding.UTF8.GetString(unclosedLangQuote.WrittenSpan);
        await Assert.That(unclosedBodyStr).Contains("lang=\"fr\">");
        await Assert.That(unclosedBodyStr).DoesNotContain("x-default");

        var badLangQuote = new ArrayBufferWriter<byte>();
        I18nHtmlRewriter.Rewrite("<html lang=\"unclosed><head></head></html>"u8, FrCode, EnCode, [], I18nOptions.Default.WithoutHreflang().WithoutLanguageSwitcher(), badLangQuote);
        await Assert.That(Encoding.UTF8.GetString(badLangQuote.WrittenSpan)).Contains("lang=\"fr\">");
    }

    /// <summary>Verifies <see cref="I18nSitemapEnricher"/> handles missing/existing <c>xmlns:xhtml</c>, unclosed tags, and non-matching URLs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task I18nSitemapEnricher_CoversExistingNamespaceAndEdgeXml()
    {
        LocaleAlternateLink[] alts =
        [
            new(FrCode, "FR"u8.ToArray(), "https://a.test/fr/"u8.ToArray()),
        ];
        Dictionary<string, LocaleAlternateLink[]> map = [with(StringComparer.Ordinal)];
        map[Encoding.UTF8.GetString(alts[0].Href)] = alts;

        var alreadyNamespaced = "<urlset xmlns:xhtml=\"http://www.w3.org/1999/xhtml\"><url><loc>https://a.test/fr/</loc></url><url><loc>https://a.test/other/</loc></url></urlset>"u8;
        var enriched = Encoding.UTF8.GetString(I18nSitemapEnricher.Enrich(alreadyNamespaced, map, EnCode));
        await Assert.That(enriched).Contains("hreflang=\"fr\"");
        await Assert.That(enriched).DoesNotContain("x-default");

        await Assert.That(Encoding.UTF8.GetString(I18nSitemapEnricher.Enrich("<root><loc>unclosed"u8, map, EnCode))).IsEqualTo("<root><loc>unclosed");
        await Assert.That(Encoding.UTF8.GetString(I18nSitemapEnricher.Enrich("<urlset attr"u8, map, EnCode))).IsEqualTo("<urlset attr");
    }

    /// <summary>Verifies notice frontmatter splicing on Markdown with and without YAML frontmatter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrependNoticePreservingFrontMatter_HandlesAllFrontMatterShapes()
    {
        var raw = "# Title"u8.ToArray();
        await Assert.That(I18nPlugin.PrependNoticePreservingFrontMatter(raw, default)).IsSameReferenceAs(raw);

        var prependedPlain = Encoding.UTF8.GetString(I18nPlugin.PrependNoticePreservingFrontMatter(raw, NoticeBytes));
        await Assert.That(prependedPlain).IsEqualTo("> Notice\n\n# Title");

        var frontMatterUnix = "---\ntitle: T\n---\nBody"u8.ToArray();
        var prependedFm = Encoding.UTF8.GetString(I18nPlugin.PrependNoticePreservingFrontMatter(frontMatterUnix, NoticeBytes));
        await Assert.That(prependedFm).StartsWith("---\ntitle: T\n---\n\n> Notice\n\nBody");

        var frontMatterCrlf = "---\r\ntitle: T\r\n---"u8.ToArray();
        var prependedEof = Encoding.UTF8.GetString(I18nPlugin.PrependNoticePreservingFrontMatter(frontMatterCrlf, NoticeBytes));
        await Assert.That(prependedEof).Contains("> Notice");

        var frontMatterUnclosed = "---\ntitle: T\nBody"u8.ToArray();
        var prependedUnclosed = Encoding.UTF8.GetString(I18nPlugin.PrependNoticePreservingFrontMatter(frontMatterUnclosed, NoticeBytes));
        await Assert.That(prependedUnclosed).StartsWith("> Notice\n\n---\n");
    }
}
