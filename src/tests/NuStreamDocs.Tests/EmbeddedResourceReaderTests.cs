// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the EmbeddedResourceReader helper used by ExtraAssetsPlugin.</summary>
public class EmbeddedResourceReaderTests
{
    /// <summary>A known embedded resource is read back as the original UTF-8 bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsKnownResource()
    {
        var source = ExtraAssetSource.Embedded(typeof(EmbeddedResourceReaderTests).Assembly, "NuStreamDocs.Tests.sample.txt", "sample.txt");
        var bytes = EmbeddedResourceReader.Read(source);
        await Assert.That(Encoding.UTF8.GetString(bytes)).IsEqualTo("hello-from-embedded");
    }

    /// <summary>A missing resource throws <see cref="InvalidOperationException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingResourceThrows()
    {
        var source = ExtraAssetSource.Embedded(typeof(EmbeddedResourceReaderTests).Assembly, "NuStreamDocs.Tests.does-not-exist.txt", "missing.txt");
        await Assert.That(() => EmbeddedResourceReader.Read(source)).Throws<InvalidOperationException>();
    }

    /// <summary>Repeated reads of the same resource produce equal byte arrays.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepeatedReadsAreStable()
    {
        var source = ExtraAssetSource.Embedded(typeof(EmbeddedResourceReaderTests).Assembly, "NuStreamDocs.Tests.sample.txt", "sample.txt");
        var first = EmbeddedResourceReader.Read(source);
        var second = EmbeddedResourceReader.Read(source);
        await Assert.That(first.AsSpan().SequenceEqual(second)).IsTrue();
    }
}
