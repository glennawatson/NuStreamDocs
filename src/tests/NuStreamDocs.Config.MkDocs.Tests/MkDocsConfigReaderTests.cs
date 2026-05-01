// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Building;

namespace NuStreamDocs.Config.MkDocs.Tests;

/// <summary>End-to-end tests for the mkdocs reader plus its builder integration.</summary>
public class MkDocsConfigReaderTests
{
    /// <summary>Reading a minimal mkdocs.yml should populate the canonical fields.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadsMinimalConfig()
    {
        var yaml = "site_name: Hello\ntheme:\n  name: material\n"u8;
        var reader = new MkDocsConfigReader();

        var config = reader.Read(yaml);

        await Assert.That(config.SiteName).IsEqualTo("Hello");
        await Assert.That(config.ThemeName).IsEqualTo("material");
    }

    /// <summary>The .yml extension should be recognised; .toml should not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RecognisesYamlExtensions()
    {
        var reader = new MkDocsConfigReader();
        await Assert.That(reader.RecognisesExtension(".yml")).IsTrue();
        await Assert.That(reader.RecognisesExtension(".yaml")).IsTrue();
        await Assert.That(reader.RecognisesExtension(".toml")).IsFalse();
    }

    /// <summary>The async stream overload should produce the same result as the span overload.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Test deliberately exercises the IConfigReader.ReadAsync default-interface-method path.")]
    public async Task ReadAsyncMatchesSpanOverload()
    {
        byte[] bytes = [.. "site_name: From-Stream\ntheme: material\n"u8];
        IConfigReader reader = new MkDocsConfigReader();

        await using var stream = new MemoryStream(bytes);
        var fromStream = await reader.ReadAsync(stream, CancellationToken.None);
        var fromSpan = reader.Read(bytes);

        await Assert.That(fromStream.SiteName).IsEqualTo(fromSpan.SiteName);
        await Assert.That(fromStream.ThemeName).IsEqualTo(fromSpan.ThemeName);
    }

    /// <summary>Builder.UseMkDocsConfig should register the reader so FindConfigReader resolves it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuilderRegistersReader()
    {
        var builder = new DocBuilder().UseMkDocsConfig();
        var reader = builder.FindConfigReader(".yml");
        await Assert.That(reader).IsNotNull();
        await Assert.That(reader!.FormatName).IsEqualTo("mkdocs");
    }
}
