// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Csp.Tests;

/// <summary>Coverage for <c>CspOptionsExtensions</c>.</summary>
public class CspOptionsExtensionsTests
{
    /// <summary>The defaults: enforce mode, hash scripts, don't hash styles, <c>'self'</c> base directives, on.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Defaults()
    {
        var o = CspOptions.Default;
        await Assert.That(o.Enabled).IsTrue();
        await Assert.That(o.Mode).IsEqualTo(CspMode.Enforce);
        await Assert.That(o.HashInlineScripts).IsTrue();
        await Assert.That(o.HashInlineStyles).IsFalse();
        await Assert.That(o.UpgradeInsecureRequests).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(o.DefaultSrc)).IsEqualTo("'self'");
        await Assert.That(o.ReportUri.Length).IsEqualTo(0);
        await Assert.That(o.ExtraSources.Length).IsEqualTo(0);
        await Assert.That(o.ExtraDirectives.Length).IsEqualTo(0);
    }

    /// <summary>The fluent setters round-trip.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SettersRoundTrip()
    {
        var o = CspOptions.Default
            .WithDefaultSrc("'none'"u8)
            .WithFrameAncestors("'none'"u8)
            .WithReportUri("/csp-report"u8)
            .WithReportOnly()
            .WithoutScriptHashing()
            .WithInlineStyleHashing()
            .WithUpgradeInsecureRequests()
            .AllowSource("script-src"u8, "https://plausible.io"u8)
            .WithExtraDirective("worker-src 'self'"u8);
        await Assert.That(Encoding.UTF8.GetString(o.DefaultSrc)).IsEqualTo("'none'");
        await Assert.That(Encoding.UTF8.GetString(o.FrameAncestors)).IsEqualTo("'none'");
        await Assert.That(Encoding.UTF8.GetString(o.ReportUri)).IsEqualTo("/csp-report");
        await Assert.That(o.Mode).IsEqualTo(CspMode.ReportOnly);
        await Assert.That(o.HashInlineScripts).IsFalse();
        await Assert.That(o.HashInlineStyles).IsTrue();
        await Assert.That(o.UpgradeInsecureRequests).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(o.ExtraSources[0].Directive)).IsEqualTo("script-src");
        await Assert.That(Encoding.UTF8.GetString(o.ExtraSources[0].Source)).IsEqualTo("https://plausible.io");
        await Assert.That(Encoding.UTF8.GetString(o.ExtraDirectives[0])).IsEqualTo("worker-src 'self'");
        await Assert.That(o.Disable().Enabled).IsFalse();
    }
}
