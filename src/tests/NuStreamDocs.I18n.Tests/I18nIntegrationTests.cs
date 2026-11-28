// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Sitemap;
using NuStreamDocs.Theme.Material3;

namespace NuStreamDocs.I18n.Tests;

/// <summary>End-to-end and lifecycle tests for <see cref="I18nPlugin"/> and <see cref="DocBuilderI18nExtensions"/>.</summary>
public sealed class I18nIntegrationTests
{
    /// <summary>English locale code.</summary>
    private const string EnLocale = "en";

    /// <summary>French locale code.</summary>
    private const string FrLocale = "fr";

    /// <summary>Japanese locale code.</summary>
    private const string JaLocale = "ja";

    /// <summary>Shared index Markdown filename.</summary>
    private const string IndexFile = "index.md";

    /// <summary>Shared normalized test site URL.</summary>
    private const string SampleSiteUrl = "https://x.test/";

    /// <summary>Gets the UTF-8 bytes of <see cref="SampleSiteUrl"/>.</summary>
    private static ReadOnlySpan<byte> SampleSiteUrlBytes => "https://x.test/"u8;

    /// <summary>Verifies multi-locale build synthesizes missing translations, rewrites <c>&lt;html lang&gt;</c>, injects switcher and <c>hreflang</c>, and enriches <c>sitemap.xml</c>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocBuilder_UseI18n_SynthesizesFallbackSwitcherHreflangAndSitemap(CancellationToken cancellationToken)
    {
        var tempRoot = new DirectoryPath(Path.Combine(Path.GetTempPath(), $"nd-i18n-{Guid.NewGuid():N}"));
        try
        {
            var inputDir = (tempRoot / "docs").Create();
            var englishDir = (inputDir / EnLocale).Create();
            var frenchDir = (inputDir / FrLocale).Create();
            var hiddenDir = (inputDir / ".hidden").Create();
            var outputDir = tempRoot / "site";

            await inputDir.File(".dotfile.md").WriteAllBytesAsync("# Ignored"u8.ToArray(), cancellationToken);
            await hiddenDir.File("secret.md").WriteAllBytesAsync("# Ignored"u8.ToArray(), cancellationToken);
            await englishDir.File(IndexFile).WriteAllBytesAsync("# Welcome\n\nEnglish home."u8.ToArray(), cancellationToken);
            await englishDir.File("setup.md").WriteAllBytesAsync("# Setup\n\nEnglish setup."u8.ToArray(), cancellationToken);
            await frenchDir.File(IndexFile).WriteAllBytesAsync("# Bienvenue\n\nAccueil français."u8.ToArray(), cancellationToken);
            await frenchDir.File("only-fr.md").WriteAllBytesAsync("# Seulement FR"u8.ToArray(), cancellationToken);

            await new DocBuilder()
                .WithInput(inputDir)
                .WithOutput(outputDir)
                .WithSiteName("I18n Docs"u8)
                .WithSiteUrl("https://docs.example.com"u8)
                .UseDirectoryUrls()
                .UseMaterial3Theme()
                .UseSitemap()
                .UseI18n(static opts => opts
                    .AddLocale(EnLocale, "English"u8)
                    .AddLocale(FrLocale, "Français"u8)
                    .AddLocale(JaLocale, "日本語"u8)
                    .WithUntranslatedNotice("> Fallback translation."u8))
                .BuildAsync(cancellationToken);

            var frenchIndexHtml = Encoding.UTF8.GetString(await (outputDir / FrLocale).File("index.html").ReadAllBytesAsync(cancellationToken));
            await Assert.That(frenchIndexHtml).Contains("lang=\"fr\"");
            await Assert.That(frenchIndexHtml).Contains("hreflang=\"en\" href=\"https://docs.example.com/en/\"");
            await Assert.That(frenchIndexHtml).Contains("hreflang=\"fr\" href=\"https://docs.example.com/fr/\"");
            await Assert.That(frenchIndexHtml).Contains("hreflang=\"ja\" href=\"https://docs.example.com/ja/\"");
            await Assert.That(frenchIndexHtml).Contains("hreflang=\"x-default\" href=\"https://docs.example.com/en/\"");
            await Assert.That(frenchIndexHtml).Contains("class=\"nd-lang-switcher\"");
            await Assert.That(frenchIndexHtml).Contains("aria-current=\"page\">Français</a>");

            var japaneseSetupHtml = Encoding.UTF8.GetString(await (outputDir / JaLocale / "setup").File("index.html").ReadAllBytesAsync(cancellationToken));
            await Assert.That(japaneseSetupHtml).Contains("Fallback translation.");
            await Assert.That(japaneseSetupHtml).Contains("lang=\"ja\"");

            var sitemapXml = Encoding.UTF8.GetString(await outputDir.File("sitemap.xml").ReadAllBytesAsync(cancellationToken));
            await Assert.That(sitemapXml).Contains("xmlns:xhtml=\"http://www.w3.org/1999/xhtml\"");
            await Assert.That(sitemapXml).Contains("<xhtml:link rel=\"alternate\" hreflang=\"ja\" href=\"https://docs.example.com/ja/\"/>");
        }
        finally
        {
            if (tempRoot.Exists())
            {
                tempRoot.DeleteRecursive(true);
            }
        }
    }

    /// <summary>Verifies <see cref="I18nPlugin"/> guard paths, disabled state, single-locale behavior, and <see cref="DocBuilderI18nExtensions"/> overloads.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PluginGuardsAndBuilderOverloads_CoverDisabledAndMissingFileBranches(CancellationToken cancellationToken)
    {
        var builder = new DocBuilder().UseI18n().UseI18n(I18nOptions.Default);
        await Assert.That(builder).IsNotNull();

        Func<I18nOptions, I18nOptions>? nullConfigure = null;
        await Assert.That(() => new DocBuilder().UseI18n(nullConfigure!)).Throws<ArgumentNullException>();

        var defaultPlugin = new I18nPlugin();
        var nameMatches = defaultPlugin.Name.SequenceEqual("i18n"u8);
        await Assert.That(nameMatches).IsTrue();
        await Assert.That(defaultPlugin.ConfigurePriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(defaultPlugin.DiscoverPriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(defaultPlugin.PostRenderPriority.Band).IsEqualTo(PluginBand.Latest);
        await Assert.That(defaultPlugin.FinalizePriority.Band).IsEqualTo(PluginBand.Late);
        await Assert.That(defaultPlugin.StaticAssets.Length).IsEqualTo(1);
        await Assert.That(defaultPlugin.NeedsRewrite("<html></html>"u8)).IsTrue();
        await Assert.That(defaultPlugin.NeedsRewrite("<head></head>"u8)).IsTrue();
        await Assert.That(defaultPlugin.NeedsRewrite("<p>plain</p>"u8)).IsFalse();

        await Assert.That(I18nPlugin.NormalizeBaseUrl("   "u8).Length).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(I18nPlugin.NormalizeBaseUrl(SampleSiteUrlBytes))).IsEqualTo(SampleSiteUrl);
        await Assert.That(Encoding.UTF8.GetString(I18nPlugin.NormalizeBaseUrl("https://x.test"u8))).IsEqualTo(SampleSiteUrl);

        var disabled = new I18nPlugin(I18nOptions.Default.Disable());
        await Assert.That(disabled.StaticAssets.Length).IsEqualTo(0);
        await Assert.That(disabled.NeedsRewrite("<html></html>"u8)).IsFalse();

        var disabledHead = new ArrayBufferWriter<byte>();
        disabled.WriteHeadExtra(disabledHead);
        await Assert.That(disabledHead.WrittenCount).IsEqualTo(0);

        var enabledHead = new ArrayBufferWriter<byte>();
        defaultPlugin.WriteHeadExtra(enabledHead);
        await Assert.That(enabledHead.WrittenCount).IsGreaterThan(0);

        var disabledOut = new ArrayBufferWriter<byte>();
        disabled.PostRender(new(IndexFile, default, "<p>x</p>"u8, disabledOut));
        await Assert.That(Encoding.UTF8.GetString(disabledOut.WrittenSpan)).IsEqualTo("<p>x</p>");

        var sink = new SyntheticPageSink();
        await disabled.DiscoverAsync(new(default, default, [], sink), cancellationToken);
        await defaultPlugin.DiscoverAsync(new("/nonexistent/nd-i18n-missing", default, [], sink), cancellationToken);
        await disabled.FinalizeAsync(new(default, []), cancellationToken);

        var tempDir = new DirectoryPath(Path.Combine(Path.GetTempPath(), $"nd-i18n-guard-{Guid.NewGuid():N}")).Create();
        try
        {
            await tempDir.File(IndexFile).WriteAllBytesAsync("# Solo"u8.ToArray(), cancellationToken);
            await defaultPlugin.ConfigureAsync(new(tempDir, tempDir, [], new()) { SiteUrl = SampleSiteUrlBytes.ToArray() }, cancellationToken);
            await defaultPlugin.DiscoverAsync(new(tempDir, tempDir, [], sink), cancellationToken);
            await defaultPlugin.FinalizeAsync(new(tempDir, []), cancellationToken);

            var noFallbackPlugin = new I18nPlugin(I18nOptions.Default.AddLocale(FrLocale, "FR"u8).WithoutFallback());
            await noFallbackPlugin.DiscoverAsync(new(tempDir, tempDir, [], sink), cancellationToken);

            await tempDir.File("sitemap.xml").WriteAllBytesAsync("<urlset></urlset>"u8.ToArray(), cancellationToken);
            await defaultPlugin.FinalizeAsync(new(tempDir, []), cancellationToken);
        }
        finally
        {
            if (tempDir.Exists())
            {
                tempDir.DeleteRecursive(true);
            }
        }
    }
}
