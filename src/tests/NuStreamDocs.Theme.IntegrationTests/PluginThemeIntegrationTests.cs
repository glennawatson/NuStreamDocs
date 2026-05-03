// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Emoji;
using NuStreamDocs.Highlight;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Nav;
using NuStreamDocs.Theme.Material;
using NuStreamDocs.Theme.Material3;
using NuStreamDocs.Toc;

namespace NuStreamDocs.Theme.IntegrationTests;

/// <summary>End-to-end render tests that confirm each plugin's output flows through both Material 2 and Material 3 page templates.</summary>
public class PluginThemeIntegrationTests
{
    /// <summary>Theme variants the parameterized tests run against.</summary>
    public enum ThemeKind
    {
        /// <summary>Classic Material (mkdocs-material 9.x).</summary>
        Material,

        /// <summary>Material 3.</summary>
        Material3,
    }

    /// <summary>A fenced csharp code block emits Pygments-class token spans plus the highlight wrapper, on both themes.</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task HighlightFenceProducesTokenSpans(ThemeKind theme)
    {
        const string Source = """
            # Hi

            ```csharp
            public class Foo { }
            ```
            """;

        var html = await BuildPageAsync(theme, Source, b => b.UseHighlight());
        await Assert.That(html).Contains("class=\"highlight\"");
        await Assert.That(html).Contains("language-csharp");

        // Token spans emit Pygments-shape classes — k = keyword, kd = keyword-declaration, kt = keyword-type.
        // The C# lexer classifies `public` / `class` as keyword-declaration (`kd`) rather than plain keyword.
        await Assert.That(html).Contains("<span class=\"k");
    }

    /// <summary>An admonition block emits the standard admonition wrapper on both themes.</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task AdmonitionRendersWithStandardClasses(ThemeKind theme)
    {
        const string Source = """
            # Hi

            !!! note
                A note body.
            """;

        var html = await BuildPageAsync(theme, Source, b => b.UseAdmonitions());
        await Assert.That(html).Contains("class=\"admonition note\"");
        await Assert.That(html).Contains("admonition-title");
    }

    /// <summary>A pymdownx-style tabbed block emits the <c>tabbed-set</c> wrapper on both themes.</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task TabsRenderTabbedSet(ThemeKind theme)
    {
        const string Source = """
            # Hi

            === "First"
                first body

            === "Second"
                second body
            """;

        var html = await BuildPageAsync(theme, Source, b => b.UseTabs());
        await Assert.That(html).Contains("tabbed-set");
        await Assert.That(html).Contains("First");
        await Assert.That(html).Contains("Second");
    }

    /// <summary>A bullet list emits <c>&lt;ul&gt;</c> + <c>&lt;li&gt;</c> on both themes (covers the recent emitter bug fix).</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task BulletListRenders(ThemeKind theme)
    {
        const string Source = """
            # Hi

            - first
            - second
            - third
            """;

        var html = await BuildPageAsync(theme, Source, _ => { });
        await Assert.That(html).Contains("<ul");
        await Assert.That(html).Contains("<li>first");
        await Assert.That(html).Contains("<li>third");
    }

    /// <summary>A top-level <c>---</c> emits an <c>&lt;hr /&gt;</c> on both themes (covers ThematicBreak emitter case).</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task ThematicBreakRendersHr(ThemeKind theme)
    {
        const string Source = """
            # Hi

            before

            ---

            after
            """;

        var html = await BuildPageAsync(theme, Source, _ => { });
        await Assert.That(html).Contains("<hr />");
    }

    /// <summary>The <c>navigation.tabs</c> shortcut produces an <c>md-tabs</c> bar in the page shell on both themes.</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task NavTabsRenderInPageShell(ThemeKind theme)
    {
        const string Source = "# Index";
        var html = await BuildPageAsync(
            theme,
            Source,
            b => b.UseNav(static opts => opts with { Tabs = true }),
            extraPages:
            [
                ("first.md", "# First"),
                ("second.md", "# Second"),
            ]);

        await Assert.That(html).Contains("class=\"md-tabs\"");
        await Assert.That(html).Contains("md-tabs__list");
    }

    /// <summary>Material 3 renders inline SVG header controls and a palette toggle in the page shell.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Material3HeaderUsesSvgControls()
    {
        const string Source = "# Index";
        var html = await BuildPageAsync(ThemeKind.Material3, Source, static _ => { });

        await Assert.That(html).Contains("data-md-component=\"palette-toggle\"");
        await Assert.That(html).Contains("<svg viewBox=\"0 0 24 24\"");
    }

    /// <summary>An emoji shortcode expands to an inline span on both themes.</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task EmojiShortcodeExpands(ThemeKind theme)
    {
        const string Source = """
            # Hi

            Hello :smile: world
            """;

        var html = await BuildPageAsync(theme, Source, b => b.UseEmoji());

        // The emoji plugin emits an inline span with the twemoji class family.
        await Assert.That(html).Contains("twemoji");
    }

    /// <summary>The page shell exposes a skip link, drawer toggle, primary sidebar, and footer regardless of plugin set, on both themes.</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task PageShellExposesCoreScaffolding(ThemeKind theme)
    {
        const string Source = "# Hi";
        var html = await BuildPageAsync(theme, Source, _ => { });

        await Assert.That(html).Contains("class=\"md-skip\"");
        await Assert.That(html).Contains("class=\"md-overlay\"");
        await Assert.That(html).Contains("md-header");
        await Assert.That(html).Contains("md-footer");
        await Assert.That(html).Contains("md-sidebar--primary");
    }

    /// <summary>The TOC shell renders a single secondary nav container and uses the on-page label on both themes.</summary>
    /// <param name="theme">Theme variant under test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(ThemeKind.Material)]
    [Arguments(ThemeKind.Material3)]
    public async Task TocRendersSingleSecondaryNav(ThemeKind theme)
    {
        const string Source = """
            # Hi

            ## Alpha

            body

            ### Beta
            """;

        var html = await BuildPageAsync(theme, Source, b => b.UseToc());
        await Assert.That(CountOccurrences(html, "class=\"md-nav md-nav--secondary\"")).IsEqualTo(1);
        await Assert.That(html).Contains("aria-label=\"On this page\"");
    }

    /// <summary>Builds a single-page site through the chosen theme and returns the rendered HTML for <c>index.md</c>.</summary>
    /// <param name="theme">Theme variant.</param>
    /// <param name="source">Markdown body for <c>index.md</c>.</param>
    /// <param name="configure">Caller-supplied plugin registration (e.g. <c>b => b.UseHighlight()</c>).</param>
    /// <param name="extraPages">Optional extra pages dropped into the docs tree before the build runs.</param>
    /// <returns>The rendered HTML.</returns>
    private static async Task<string> BuildPageAsync(
        ThemeKind theme,
        string source,
        Action<DocBuilder> configure,
        (string Path, string Source)[]? extraPages = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "index.md"), source);
        if (extraPages is not null)
        {
            for (var i = 0; i < extraPages.Length; i++)
            {
                var (path, body) = extraPages[i];
                var target = Path.Combine(fixture.Docs, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, body);
            }
        }

        var builder = new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site);

        switch (theme)
        {
            case ThemeKind.Material:
            {
                builder.UseMaterialTheme();
                break;
            }

            case ThemeKind.Material3:
            {
                builder.UseMaterial3Theme();
                break;
            }

            default:
            {
                throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }

        configure(builder);
        await builder.BuildAsync();

        return await File.ReadAllTextAsync(Path.Combine(fixture.Site, "index.html"));
    }

    /// <summary>Counts the ordinal occurrences of <paramref name="value"/> inside <paramref name="text"/>.</summary>
    /// <param name="text">Source text.</param>
    /// <param name="value">Substring to count.</param>
    /// <returns>The number of matches.</returns>
    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += value.Length;
        }
    }
}
