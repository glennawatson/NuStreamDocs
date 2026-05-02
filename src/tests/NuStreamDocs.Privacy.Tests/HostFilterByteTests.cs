// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Direct tests for the byte-only HostFilter overload — the production hot path that decides without UTF-16 transcoding for the scheme + skip-list + allow-list common cases.</summary>
public class HostFilterByteTests
{
    /// <summary>Non-http(s) schemes are rejected without consulting the allow/skip lists.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RejectsNonHttpScheme()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        await Assert.That(filter.ShouldLocalize("ftp://example.com/x"u8)).IsFalse();
        await Assert.That(filter.ShouldLocalize("data:image/png;base64,abc"u8)).IsFalse();
        await Assert.That(filter.ShouldLocalize("/relative/path"u8)).IsFalse();
    }

    /// <summary>An empty / no-host URL is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RejectsHostlessUrl()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        await Assert.That(filter.ShouldLocalize("https://"u8)).IsFalse();
        await Assert.That(filter.ShouldLocalize(default(ReadOnlySpan<byte>))).IsFalse();
    }

    /// <summary>Host on the skip list rejects regardless of allow list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipListRejects()
    {
        var filter = new HostFilter(hostsToSkip: ["analytics.example"], hostsAllowed: null);
        await Assert.That(filter.ShouldLocalize("https://analytics.example/track"u8)).IsFalse();
        await Assert.That(filter.ShouldLocalize("https://other.example/x"u8)).IsTrue();
    }

    /// <summary>Skip-list match is case-insensitive.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipListIgnoresCase()
    {
        var filter = new HostFilter(hostsToSkip: ["Analytics.Example"], hostsAllowed: null);
        await Assert.That(filter.ShouldLocalize("https://ANALYTICS.example/x"u8)).IsFalse();
    }

    /// <summary>Allow list restricts when set; non-listed hosts reject.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AllowListRestricts()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: ["only.example"]);
        await Assert.That(filter.ShouldLocalize("https://only.example/x"u8)).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://other.example/x"u8)).IsFalse();
    }

    /// <summary>Pattern-only filter forces the string follow-up; an exclude pattern rejects, an include pattern accepts.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PatternFollowupHandlesExclude()
    {
        var filter = new HostFilter(
            hostsToSkip: null,
            hostsAllowed: null,
            includePatterns: null,
            excludePatterns: ["*tracking*"]);
        await Assert.That(filter.ShouldLocalize("https://x.example/tracking/pixel"u8)).IsFalse();
        await Assert.That(filter.ShouldLocalize("https://x.example/normal"u8)).IsTrue();
    }

    /// <summary>String adapter delegates to the byte path with no behavioral change.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StringAdapterMatchesByteOverload()
    {
        var filter = new HostFilter(hostsToSkip: ["skip.example"], hostsAllowed: ["allow.example"]);
        await Assert.That(filter.ShouldLocalize("https://allow.example/a")).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://skip.example/a")).IsFalse();
        await Assert.That(filter.ShouldLocalize("https://nope.example/a")).IsFalse();
    }

    /// <summary>Userinfo prefix in the URL is stripped before host matching.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StripsUserinfoBeforeHostMatch()
    {
        var filter = new HostFilter(hostsToSkip: ["example.com"], hostsAllowed: null);
        await Assert.That(filter.ShouldLocalize("https://user:pass@example.com/x"u8)).IsFalse();
    }

    /// <summary>Port suffix doesn't break host matching.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IgnoresPortInHostMatch()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: ["example.com"]);
        await Assert.That(filter.ShouldLocalize("https://example.com:8443/x"u8)).IsTrue();
    }
}
