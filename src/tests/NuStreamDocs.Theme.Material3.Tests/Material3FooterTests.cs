// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>End-to-end tests covering the rich-HTML copyright passthrough and the social-link list rendered by the Material 3 footer.</summary>
public class Material3FooterTests
{
    /// <summary>When <c>WithCopyrightHtml</c> is set, the footer renders the supplied bytes verbatim and skips the plain-text <c>md-copyright</c> wrapper.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CopyrightHtmlRendersVerbatim()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts
                .WithSiteName("Site"u8)
                .WithCopyright("plain text fallback"u8)
                .WithCopyrightHtml("<div class=\"md-copyright\"><a href=\"/legal/\">Legal</a> | © Acme</div>"u8))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("<div class=\"md-copyright\"><a href=\"/legal/\">Legal</a> | © Acme</div>");
        await Assert.That(html).DoesNotContain("plain text fallback");
        await Assert.That(html).DoesNotContain("&lt;a href=");
    }

    /// <summary>When <c>AddSocialLink</c> is called, the rendered footer includes a <c>md-social</c> wrapper with one anchor per link, raw SVG bytes intact.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SocialLinksRenderInFooter()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts
                .WithSiteName("Site"u8)
                .AddSocialLink(
                    "https://github.com/example"u8.ToArray(),
                    "Example on Github"u8.ToArray(),
                    "<svg id=\"github-svg\"><path d=\"M0 0\"/></svg>"u8.ToArray())
                .AddSocialLink(
                    "https://discord.example.org"u8.ToArray(),
                    "Example on Discord"u8.ToArray(),
                    "<svg id=\"discord-svg\"><path d=\"M1 1\"/></svg>"u8.ToArray()))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("<div class=\"md-social\">");
        await Assert.That(html).Contains("href=\"https://github.com/example\"");
        await Assert.That(html).Contains("title=\"Example on Github\"");
        await Assert.That(html).Contains("class=\"md-social__link\"");
        await Assert.That(html).Contains("<svg id=\"github-svg\"><path d=\"M0 0\"/></svg>");
        await Assert.That(html).Contains("href=\"https://discord.example.org\"");
        await Assert.That(html).Contains("title=\"Example on Discord\"");
        await Assert.That(html).Contains("<svg id=\"discord-svg\"><path d=\"M1 1\"/></svg>");
        await Assert.That(html).DoesNotContain("&lt;svg");
    }

    /// <summary>The plain-text copyright path remains intact when no rich HTML or social links are configured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainCopyrightStillRendersWithoutRichHtmlOrSocial()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts
                .WithSiteName("Site"u8)
                .WithCopyright("(c) Acme Corp"u8))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("<div class=\"md-copyright\">(c) Acme Corp</div>");
        await Assert.That(html).DoesNotContain("md-social");
    }
}
