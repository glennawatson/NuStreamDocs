// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behavior tests for <c>PrivacyOptionsExtensions</c>'s host-list helpers.</summary>
public class PrivacyOptionsExtensionsTests
{
    /// <summary><see cref="PrivacyOptions.Default"/> ships with the well-known never-localize hosts populated, so consumers don't have to repeat the boilerplate.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptionsCarryWellKnownSkipHosts()
    {
        var hosts = Decode(PrivacyOptions.Default.HostsToSkip);
        await Assert.That(hosts).Contains("github.com");
        await Assert.That(hosts).Contains("raw.githubusercontent.com");
        await Assert.That(hosts).Contains("twitter.com");
        await Assert.That(hosts).Contains("x.com");
        await Assert.That(hosts).Contains("youtube.com");
    }

    /// <summary><c>WithHostsToSkip(string[])</c> replaces the entire skip list, dropping the well-known defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithHostsToSkipReplacesTheList()
    {
        var updated = PrivacyOptions.Default.WithHostsToSkip("custom.example");
        var hosts = Decode(updated.HostsToSkip);
        await Assert.That(hosts.Length).IsEqualTo(1);
        await Assert.That(hosts[0]).IsEqualTo("custom.example");
    }

    /// <summary><c>AddHostsToSkip(string[])</c> appends to the existing list, preserving the well-known defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddHostsToSkipAppendsToExistingList()
    {
        var defaultCount = PrivacyOptions.Default.HostsToSkip.Length;
        var updated = PrivacyOptions.Default.AddHostsToSkip("reactivex.slack.com", "discord.gg");

        await Assert.That(updated.HostsToSkip.Length).IsEqualTo(defaultCount + 2);
        var hosts = Decode(updated.HostsToSkip);
        await Assert.That(hosts).Contains("github.com");
        await Assert.That(hosts).Contains("reactivex.slack.com");
        await Assert.That(hosts).Contains("discord.gg");
    }

    /// <summary><c>AddHostsToSkip(string[])</c> with an empty array returns the source unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddHostsToSkipWithEmptyInputIsNoOp()
    {
        var updated = PrivacyOptions.Default.AddHostsToSkip(System.Array.Empty<string>());
        await Assert.That(updated.HostsToSkip).IsEqualTo(PrivacyOptions.Default.HostsToSkip);
    }

    /// <summary><c>ClearHostsToSkip</c> empties the list — drops both defaults and any caller-added entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearHostsToSkipEmptiesTheList()
    {
        var updated = PrivacyOptions.Default
            .AddHostsToSkip("custom.example")
            .ClearHostsToSkip();
        await Assert.That(updated.HostsToSkip.Length).IsEqualTo(0);
    }

    /// <summary>The UTF-8 byte overload of <c>WithHostsToSkip</c> stores the raw bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithHostsToSkipByteOverloadStoresBytesVerbatim()
    {
        byte[][] hosts = ["custom.example"u8.ToArray(), "another.example"u8.ToArray()];
        var updated = PrivacyOptions.Default.WithHostsToSkip(hosts);
        await Assert.That(updated.HostsToSkip).IsEqualTo(hosts);
    }

    /// <summary>The UTF-8 byte overload of <c>AddHostsToSkip</c> appends raw bytes to the existing list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddHostsToSkipByteOverloadAppends()
    {
        var defaultCount = PrivacyOptions.Default.HostsToSkip.Length;
        byte[][] hosts = ["custom.example"u8.ToArray()];
        var updated = PrivacyOptions.Default.AddHostsToSkip(hosts);
        await Assert.That(updated.HostsToSkip.Length).IsEqualTo(defaultCount + 1);
        var decoded = Decode(updated.HostsToSkip);
        await Assert.That(decoded).Contains("custom.example");
    }

    /// <summary><c>WithHostsAllowed(string[])</c> replaces the allow list (which is empty by default).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithHostsAllowedReplacesAllowList()
    {
        var updated = PrivacyOptions.Default.WithHostsAllowed("a.example", "b.example");
        var hosts = Decode(updated.HostsAllowed);
        await Assert.That(hosts.Length).IsEqualTo(2);
        await Assert.That(hosts[0]).IsEqualTo("a.example");
        await Assert.That(hosts[1]).IsEqualTo("b.example");
    }

    /// <summary><c>AddHostsAllowed</c> + <c>ClearHostsAllowed</c> mirror the skip-list semantics.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddAndClearHostsAllowed()
    {
        var added = PrivacyOptions.Default.AddHostsAllowed("a.example", "b.example");
        await Assert.That(added.HostsAllowed.Length).IsEqualTo(2);

        var cleared = added.ClearHostsAllowed();
        await Assert.That(cleared.HostsAllowed.Length).IsEqualTo(0);
    }

    /// <summary><c>WithUrlIncludePatterns</c> / <c>WithUrlExcludePatterns</c> support both string and byte forms.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlPatternHelpersStringAndByteForms()
    {
        var stringForm = PrivacyOptions.Default.WithUrlIncludePatterns("https://*.example/**");
        await Assert.That(stringForm.UrlIncludePatterns.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(stringForm.UrlIncludePatterns[0])).IsEqualTo("https://*.example/**");

        byte[][] excludes = [[.. "https://*.tracker/**"u8]];
        var byteForm = PrivacyOptions.Default.WithUrlExcludePatterns(excludes);
        await Assert.That(byteForm.UrlExcludePatterns).IsSameReferenceAs(excludes);

        var added = byteForm.AddUrlIncludePatterns("https://*.cdn/**");
        await Assert.That(added.UrlIncludePatterns.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(added.UrlIncludePatterns[0])).IsEqualTo("https://*.cdn/**");

        var cleared = added.ClearUrlIncludePatterns().ClearUrlExcludePatterns();
        await Assert.That(cleared.UrlIncludePatterns.Length).IsEqualTo(0);
        await Assert.That(cleared.UrlExcludePatterns.Length).IsEqualTo(0);
    }

    /// <summary>Single-path scalar helpers (<c>WithAuditManifestPath</c>, <c>WithCacheDirectory</c>, <c>WithCspManifestPath</c>) accept both strings and UTF-8 bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PathHelpersStringAndByteForms()
    {
        // String form encodes via Utf8Encoder.Encode (empty maps to empty).
        var byString = PrivacyOptions.Default
            .WithAuditManifestPath("audit.json")
            .WithCacheDirectory("/var/cache/p")
            .WithCspManifestPath("csp.json");
        await Assert.That(Encoding.UTF8.GetString(byString.AuditManifestPath)).IsEqualTo("audit.json");
        await Assert.That(Encoding.UTF8.GetString(byString.CacheDirectory)).IsEqualTo("/var/cache/p");
        await Assert.That(Encoding.UTF8.GetString(byString.CspManifestPath)).IsEqualTo("csp.json");

        // Byte form stores verbatim.
        byte[] auditBytes = [.. "a.json"u8];
        byte[] cacheBytes = [.. "/c"u8];
        byte[] cspBytes = [.. "c.json"u8];
        var byBytes = PrivacyOptions.Default
            .WithAuditManifestPath(auditBytes)
            .WithCacheDirectory(cacheBytes)
            .WithCspManifestPath(cspBytes);
        await Assert.That(byBytes.AuditManifestPath).IsSameReferenceAs(auditBytes);
        await Assert.That(byBytes.CacheDirectory).IsSameReferenceAs(cacheBytes);
        await Assert.That(byBytes.CspManifestPath).IsSameReferenceAs(cspBytes);
    }

    /// <summary>Path-helper byte overloads reject null.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PathHelperByteOverloadsRejectNull()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => PrivacyOptions.Default.WithAuditManifestPath((byte[])null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => PrivacyOptions.Default.WithCacheDirectory((byte[])null!));
        var ex3 = Assert.Throws<ArgumentNullException>(static () => PrivacyOptions.Default.WithCspManifestPath((byte[])null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
        await Assert.That(ex3).IsNotNull();
    }

    /// <summary>Single-entry <see cref="ReadOnlySpan{T}"/> overloads for the host / URL-pattern adders accept <c>"..."u8</c> literals directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadsAcceptU8LiteralsDirectly()
    {
        var defaultCount = PrivacyOptions.Default.HostsToSkip.Length;
        var updated = PrivacyOptions.Default
            .AddHostsToSkip("custom.example"u8)
            .AddHostsAllowed("allowed.example"u8)
            .AddUrlIncludePatterns("https://*.cdn/**"u8)
            .AddUrlExcludePatterns("https://*.tracker/**"u8);

        await Assert.That(updated.HostsToSkip.Length).IsEqualTo(defaultCount + 1);
        await Assert.That(updated.HostsToSkip[^1].AsSpan().SequenceEqual("custom.example"u8)).IsTrue();
        await Assert.That(updated.HostsAllowed[^1].AsSpan().SequenceEqual("allowed.example"u8)).IsTrue();
        await Assert.That(updated.UrlIncludePatterns[^1].AsSpan().SequenceEqual("https://*.cdn/**"u8)).IsTrue();
        await Assert.That(updated.UrlExcludePatterns[^1].AsSpan().SequenceEqual("https://*.tracker/**"u8)).IsTrue();
    }

    /// <summary><see cref="ReadOnlySpan{T}"/> overloads on the path scalars accept <c>"..."u8</c> literals directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PathScalarSpanOverloadsAcceptU8LiteralsDirectly()
    {
        var updated = PrivacyOptions.Default
            .WithAuditManifestPath("audit.json"u8)
            .WithCacheDirectory("/var/cache/p"u8)
            .WithCspManifestPath("csp.json"u8);
        await Assert.That(updated.AuditManifestPath.AsSpan().SequenceEqual("audit.json"u8)).IsTrue();
        await Assert.That(updated.CacheDirectory.AsSpan().SequenceEqual("/var/cache/p"u8)).IsTrue();
        await Assert.That(updated.CspManifestPath.AsSpan().SequenceEqual("csp.json"u8)).IsTrue();
    }

    /// <summary>Helper to decode <see cref="PrivacyOptions.HostsToSkip"/> / <see cref="PrivacyOptions.HostsAllowed"/> for assertion.</summary>
    /// <param name="hosts">UTF-8 host arrays.</param>
    /// <returns>Decoded host strings.</returns>
    private static string[] Decode(byte[][] hosts)
    {
        var result = new string[hosts.Length];
        for (var i = 0; i < hosts.Length; i++)
        {
            result[i] = Encoding.UTF8.GetString(hosts[i]);
        }

        return result;
    }
}
