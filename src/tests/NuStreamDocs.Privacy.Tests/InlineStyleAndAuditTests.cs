// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behavior tests for inline <c>&lt;style&gt;</c> rewriting, audit-only mode, and allow/skip-list semantics.</summary>
public class InlineStyleAndAuditTests
{
    /// <summary>Filter that accepts every host.</summary>
    private static readonly HostFilter AllHosts = new(hostsToSkip: null, hostsAllowed: null);

    /// <summary>A <c>url(...)</c> token inside an inline <c>&lt;style&gt;</c> block is registered and rewritten to a local path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesUrlInsideInlineStyle()
    {
        var registry = new ExternalAssetRegistry("assets/external"u8.ToArray());
        const string Source = "<style>body { background: url(https://example.com/bg.png) }</style>";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Source), registry, AllHosts));
        await Assert.That(output).Contains("url(/assets/external/");
        await Assert.That(output).DoesNotContain("https://example.com/bg.png");
    }

    /// <summary>Audit mode collects URLs but doesn't rewrite the HTML.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AuditModeCollectsWithoutRewriting()
    {
        var audit = new ConcurrentDictionary<byte[], byte>(ByteArrayComparer.Instance);
        const string Source = "<img src=\"https://example.com/x.png\"><style>a { background: url(https://example.com/y.png) }</style>";
        ExternalUrlScanner.Audit(Encoding.UTF8.GetBytes(Source), AllHosts, audit);
        await Assert.That(audit.ContainsKey("https://example.com/x.png"u8.ToArray())).IsTrue();
        await Assert.That(audit.ContainsKey("https://example.com/y.png"u8.ToArray())).IsTrue();
    }

    /// <summary>An empty allow-list defaults to "localize everything not on the skip list".</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyAllowListLocalizesEverything()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        await Assert.That(filter.ShouldLocalize("https://anything.example/x.png"u8)).IsTrue();
    }

    /// <summary>A non-empty allow-list excludes hosts not on it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AllowListExcludesUnlistedHosts()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: PrivacyTestHelpers.Utf8("allowed.example"));
        await Assert.That(filter.ShouldLocalize("https://allowed.example/x.png"u8)).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://other.example/x.png"u8)).IsFalse();
    }

    /// <summary>The skip-list wins over the allow-list when both contain the same host.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipListWinsOverAllowList()
    {
        var filter = new HostFilter(hostsToSkip: PrivacyTestHelpers.Utf8("x.example"), hostsAllowed: PrivacyTestHelpers.Utf8("x.example"));
        await Assert.That(filter.ShouldLocalize("https://x.example/anything"u8)).IsFalse();
    }
}
