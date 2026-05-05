// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Building;
using NuStreamDocs.Config.MkDocs;

namespace NuStreamDocs.Config.Zensical.Tests;

/// <summary>End-to-end tests for the Zensical TOML reader and its builder integration.</summary>
public class ZensicalConfigReaderTests
{
    /// <summary>A minimal TOML document should populate the canonical fields.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadsMinimalConfig()
    {
        var toml = "site_name = \"Hi\"\nsite_url = \"https://example.test\"\n\n[theme]\nname = \"material\"\n"u8;
        ZensicalConfigReader reader = new();

        var config = reader.Read(toml);

        await Assert.That(config.SiteName).IsEqualTo("Hi");
        await Assert.That(config.SiteUrl).IsEqualTo("https://example.test");
        await Assert.That(config.ThemeName).IsEqualTo("material");
    }

    /// <summary>The .toml extension should be recognized; .yml should not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RecognizesTomlExtensions()
    {
        ZensicalConfigReader reader = new();
        await Assert.That(reader.RecognizesExtension(".toml")).IsTrue();
        await Assert.That(reader.RecognizesExtension(".yml")).IsFalse();
    }

    /// <summary>Streaming overload should match the span overload byte-for-byte.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Test deliberately exercises the IConfigReader.ReadAsync default-interface-method path.")]
    public async Task ReadAsyncMatchesSpanOverload()
    {
        byte[] bytes = [.. "site_name = \"Async\"\n[theme]\nname = \"material\"\n"u8];
        IConfigReader reader = new ZensicalConfigReader();

        await using MemoryStream stream = new(bytes);
        var fromStream = await reader.ReadAsync(stream, CancellationToken.None);
        var fromSpan = reader.Read(bytes);

        await Assert.That(fromStream.SiteName).IsEqualTo(fromSpan.SiteName);
        await Assert.That(fromStream.ThemeName).IsEqualTo(fromSpan.ThemeName);
    }

    /// <summary>UseZensicalConfig with a TOML byte span applies the parsed site metadata to the builder.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UseZensicalConfigAppliesSiteMetadata()
    {
        var toml = "site_name = \"From-Toml\"\nsite_url = \"https://x.test/\"\n[theme]\nname = \"material\"\n"u8;
        var builder = new DocBuilder().UseZensicalConfig(toml);

        await Assert.That(System.Text.Encoding.UTF8.GetString(builder.SiteName())).IsEqualTo("From-Toml");
        await Assert.That(System.Text.Encoding.UTF8.GetString(builder.SiteUrl())).IsEqualTo("https://x.test/");
    }
}
