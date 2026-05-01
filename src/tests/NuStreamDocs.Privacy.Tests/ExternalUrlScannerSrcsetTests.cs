// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Direct tests for SrcsetUrlExtractor.Extract.</summary>
public class ExternalUrlScannerSrcsetTests
{
    /// <summary>URL only — no descriptor — returns the trimmed URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlOnly() =>
        await Assert.That(SrcsetUrlExtractor.Extract("  https://x.test/img.png  ")).IsEqualTo("https://x.test/img.png");

    /// <summary>URL with descriptor returns just the URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlWithDescriptor() =>
        await Assert.That(SrcsetUrlExtractor.Extract("https://x.test/img.png 2x")).IsEqualTo("https://x.test/img.png");

    /// <summary>Empty input returns empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput() => await Assert.That(SrcsetUrlExtractor.Extract(string.Empty)).IsEqualTo(string.Empty);
}
