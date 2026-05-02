// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Config;

namespace NuStreamDocs.Tests;

/// <summary>Tests for <c>DocBuilderBulkExtensions</c>.</summary>
public class DocBuilderBulkExtensionsTests
{
    /// <summary>UsePlugins registers each plugin in order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UsePluginsRegistersAll()
    {
        var builder = new DocBuilder();
        var p1 = new RecordingPlugin();
        var p2 = new RecordingPlugin();
        var returned = builder.UsePlugins(p1, p2);
        await Assert.That(returned).IsSameReferenceAs(builder);
    }

    /// <summary>UsePlugins with no entries leaves the builder unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UsePluginsEmptyIsHarmless()
    {
        var builder = new DocBuilder();
        await Assert.That(builder.UsePlugins()).IsSameReferenceAs(builder);
    }

    /// <summary>UsePlugins rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UsePluginsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderBulkExtensions.UsePlugins(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseConfigReaders registers each reader.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseConfigReadersRegistersAll()
    {
        var builder = new DocBuilder();
        var r = new TestConfigReader(".test");
        var returned = builder.UseConfigReaders(r);
        await Assert.That(returned).IsSameReferenceAs(builder);
        await Assert.That(builder.FindConfigReader(".test".AsSpan())).IsSameReferenceAs(r);
    }

    /// <summary>UseConfigReaders rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseConfigReadersRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderBulkExtensions.UseConfigReaders(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Stub config reader that recognizes a single extension.</summary>
    private sealed class TestConfigReader(string extension) : IConfigReader
    {
        /// <inheritdoc/>
        public string FormatName => "test";

        /// <inheritdoc/>
        public bool RecognizesExtension(ReadOnlySpan<char> ext) => ext.SequenceEqual(extension);

        /// <inheritdoc/>
        public MkDocsConfig Read(ReadOnlySpan<byte> utf8Source) => new("Site", null, "material", []);
    }
}
