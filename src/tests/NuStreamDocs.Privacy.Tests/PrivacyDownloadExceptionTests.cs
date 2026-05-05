// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Constructor coverage for PrivacyDownloadException.</summary>
public class PrivacyDownloadExceptionTests
{
    /// <summary>Default ctor produces an empty failed-URL list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultCtor()
    {
        PrivacyDownloadException ex = new();
        await Assert.That(ex.FailedUrls.Length).IsEqualTo(0);
    }

    /// <summary>Message ctor preserves the message and yields an empty list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MessageCtor()
    {
        PrivacyDownloadException ex = new("download failed");
        await Assert.That(ex.Message).IsEqualTo("download failed");
        await Assert.That(ex.FailedUrls.Length).IsEqualTo(0);
    }

    /// <summary>Message + inner ctor preserves both.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MessageInnerCtor()
    {
        HttpRequestException inner = new("inner");
        PrivacyDownloadException ex = new("oops", inner);
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }
}
