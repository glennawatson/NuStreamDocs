// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Coverage for PrivacyPlugin.Name and AuditedUrls.</summary>
public class PrivacyPluginCoverageTests
{
    /// <summary>Name returns "privacy".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        var plugin = new PrivacyPlugin();
        await Assert.That(plugin.Name).IsEqualTo("privacy");
    }

    /// <summary>AuditedUrls is empty before any pages are scanned.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AuditedUrlsEmpty()
    {
        var plugin = new PrivacyPlugin();
        await Assert.That(plugin.AuditedUrls.Length).IsEqualTo(0);
    }
}
