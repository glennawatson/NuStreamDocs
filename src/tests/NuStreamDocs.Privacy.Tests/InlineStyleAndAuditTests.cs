// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behaviour tests for inline <c>&lt;style&gt;</c> rewriting, audit-only mode, and allow/skip-list semantics.</summary>
public class InlineStyleAndAuditTests
{
    /// <summary>Filter that accepts every host.</summary>
    private static readonly HostFilter AllHosts = new(hostsToSkip: null, hostsAllowed: null);

    /// <summary>A <c>url(...)</c> token inside an inline <c>&lt;style&gt;</c> block is registered and rewritten to a local path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesUrlInsideInlineStyle()
    {
        var registry = new ExternalAssetRegistry("assets/external");
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
        var audit = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        const string Source = "<img src=\"https://example.com/x.png\"><style>a { background: url(https://example.com/y.png) }</style>";
        ExternalUrlScanner.Audit(Encoding.UTF8.GetBytes(Source), AllHosts, audit);
        await Assert.That(audit.Keys).Contains("https://example.com/x.png");
        await Assert.That(audit.Keys).Contains("https://example.com/y.png");
    }

    /// <summary>An empty allow-list defaults to "localise everything not on the skip list".</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyAllowListLocalisesEverything()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        await Assert.That(filter.ShouldLocalise("https://anything.example/x.png")).IsTrue();
    }

    /// <summary>A non-empty allow-list excludes hosts not on it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AllowListExcludesUnlistedHosts()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: ["allowed.example"]);
        await Assert.That(filter.ShouldLocalise("https://allowed.example/x.png")).IsTrue();
        await Assert.That(filter.ShouldLocalise("https://other.example/x.png")).IsFalse();
    }

    /// <summary>The skip-list wins over the allow-list when both contain the same host.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipListWinsOverAllowList()
    {
        var filter = new HostFilter(hostsToSkip: ["x.example"], hostsAllowed: ["x.example"]);
        await Assert.That(filter.ShouldLocalise("https://x.example/anything")).IsFalse();
    }
}
