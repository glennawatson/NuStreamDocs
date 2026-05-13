// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Csp.Tests;

/// <summary>Coverage for <see cref="CspBuilder"/>.</summary>
public class CspBuilderTests
{
    /// <summary>The default policy has the expected <c>'self'</c>-based directives; styles get <c>'unsafe-inline'</c>, scripts don't.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultPolicy()
    {
        var csp = Encoding.UTF8.GetString(CspBuilder.Build([], [], CspOptions.Default));
        await Assert.That(csp).Contains("default-src 'self'");
        await Assert.That(csp).Contains("base-uri 'self'");
        await Assert.That(csp).Contains("object-src 'none'");
        await Assert.That(csp).Contains("frame-ancestors 'self'");
        await Assert.That(csp).Contains("img-src 'self' data:");
        await Assert.That(csp).Contains("font-src 'self'");
        await Assert.That(csp).Contains("style-src 'self' 'unsafe-inline'");
        await Assert.That(csp).Contains("script-src 'self'");
        await Assert.That(csp).DoesNotContain("script-src 'self' 'unsafe-inline'");
        await Assert.That(csp).DoesNotContain("upgrade-insecure-requests");
    }

    /// <summary>Script hashes are placed in <c>script-src</c> after <c>'self'</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScriptHashesInScriptSrc()
    {
        var csp = Encoding.UTF8.GetString(
            CspBuilder.Build(
                [[.. "'sha256-ABC'"u8], [.. "'sha256-DEF'"u8]],
                [],
                CspOptions.Default));
        await Assert.That(csp).Contains("script-src 'self' 'sha256-ABC' 'sha256-DEF'");
    }

    /// <summary>Without script hashing, <c>script-src</c> uses <c>'unsafe-inline'</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithoutScriptHashingUsesUnsafeInline()
    {
        var csp = Encoding.UTF8.GetString(CspBuilder.Build([], [], CspOptions.Default.WithoutScriptHashing()));
        await Assert.That(csp).Contains("script-src 'self' 'unsafe-inline'");
    }

    /// <summary>Extra sources, extra directives, upgrade-insecure-requests and report-uri all appear.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExtrasAreAppended()
    {
        var options = CspOptions.Default
            .AllowSource("script-src"u8, "https://plausible.io"u8)
            .AllowSource("connect-src"u8, "https://api.example.com"u8)
            .WithExtraDirective("worker-src 'self'"u8)
            .WithUpgradeInsecureRequests()
            .WithReportUri("/csp-report"u8);
        var csp = Encoding.UTF8.GetString(CspBuilder.Build([], [], options));
        await Assert.That(csp).Contains("script-src 'self' https://plausible.io");
        await Assert.That(csp).Contains("connect-src https://api.example.com");
        await Assert.That(csp).Contains("worker-src 'self'");
        await Assert.That(csp).Contains("upgrade-insecure-requests");
        await Assert.That(csp).Contains("report-uri /csp-report");
    }
}
