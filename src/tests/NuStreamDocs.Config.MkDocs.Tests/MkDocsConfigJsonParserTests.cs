// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Config.MkDocs.Tests;

/// <summary>Behavior tests for <c>MkDocsConfigJsonParser</c>.</summary>
public class MkDocsConfigJsonParserTests
{
    /// <summary>An empty object yields all defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyObjectDefaults()
    {
        var config = MkDocsConfigJsonParser.FromJson("{}"u8);
        await Assert.That(config.SiteName).IsEqualTo(string.Empty);
        await Assert.That(config.SiteUrl).IsNull();
        await Assert.That(config.ThemeName).IsEqualTo("material");
        await Assert.That(config.UseDirectoryUrls).IsTrue();
    }

    /// <summary>Site fields round-trip.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SiteFieldsRoundtrip()
    {
        const string Json = "{\"site_name\":\"Docs\",\"site_url\":\"https://x.test/\",\"use_directory_urls\":false}";
        var config = MkDocsConfigJsonParser.FromJson(Encoding.UTF8.GetBytes(Json));
        await Assert.That(config.SiteName).IsEqualTo("Docs");
        await Assert.That(config.SiteUrl).IsEqualTo("https://x.test/");
        await Assert.That(config.UseDirectoryUrls).IsFalse();
    }

    /// <summary>Theme as a string sets ThemeName directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThemeStringForm()
    {
        var config = MkDocsConfigJsonParser.FromJson("{\"theme\":\"zensical\"}"u8);
        await Assert.That(config.ThemeName).IsEqualTo("zensical");
    }

    /// <summary>Theme as an object reads the <c>name</c> property.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThemeObjectForm()
    {
        var config = MkDocsConfigJsonParser.FromJson("{\"theme\":{\"name\":\"material3\"}}"u8);
        await Assert.That(config.ThemeName).IsEqualTo("material3");
    }

    /// <summary>Theme as an object without a name falls back to default.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThemeObjectWithoutName()
    {
        var config = MkDocsConfigJsonParser.FromJson("{\"theme\":{\"palette\":\"dark\"}}"u8);
        await Assert.That(config.ThemeName).IsEqualTo("material");
    }

    /// <summary>Theme of an unexpected JSON kind also falls back.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThemeUnexpectedKind()
    {
        var config = MkDocsConfigJsonParser.FromJson("{\"theme\":42}"u8);
        await Assert.That(config.ThemeName).IsEqualTo("material");
    }

    /// <summary>use_directory_urls of an unexpected kind falls back to true.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseDirectoryUrlsFallback()
    {
        var config = MkDocsConfigJsonParser.FromJson("{\"use_directory_urls\":\"not-a-bool\"}"u8);
        await Assert.That(config.UseDirectoryUrls).IsTrue();
    }
}
