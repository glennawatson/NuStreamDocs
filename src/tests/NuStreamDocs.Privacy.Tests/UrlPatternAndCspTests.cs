// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behavior tests for <c>UrlPatternMatcher</c>, URL-level filter rules, and <c>CspHashCollector</c>.</summary>
public class UrlPatternAndCspTests
{
    /// <summary><c>*</c> matches any sequence of characters and <c>?</c> matches one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GlobWildcardsMatchExpectedUrls()
    {
        var matcher = new UrlPatternMatcher(["https://fonts.googleapis.com/css*"u8.ToArray()]);
        await Assert.That(matcher.IsMatch("https://fonts.googleapis.com/css"u8)).IsTrue();
        await Assert.That(matcher.IsMatch("https://fonts.googleapis.com/css?family=Roboto"u8)).IsTrue();
        await Assert.That(matcher.IsMatch("https://fonts.googleapis.com/recaptcha/foo"u8)).IsFalse();
    }

    /// <summary>An include pattern broadens the allow set even when the host isn't on <c>HostsAllowed</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IncludePatternBroadensAllowSet()
    {
        var filter = new HostFilter(
            hostsToSkip: null,
            hostsAllowed: PrivacyTestHelpers.Utf8("allowed.example"),
            includePatterns: PrivacyTestHelpers.Utf8("https://other.example/css*"),
            excludePatterns: null);
        await Assert.That(filter.ShouldLocalize("https://allowed.example/x.png"u8)).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://other.example/css/main.css"u8)).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://other.example/recaptcha/x"u8)).IsFalse();
    }

    /// <summary>An exclude pattern wins over a host that would otherwise pass.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExcludePatternBlocksAllowedHost()
    {
        var filter = new HostFilter(
            hostsToSkip: null,
            hostsAllowed: null,
            includePatterns: null,
            excludePatterns: PrivacyTestHelpers.Utf8("https://*.googleapis.com/recaptcha/*"));
        await Assert.That(filter.ShouldLocalize("https://fonts.googleapis.com/css"u8)).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://fonts.googleapis.com/recaptcha/x"u8)).IsFalse();
    }

    /// <summary>Inline <c>&lt;style&gt;</c> bodies produce stable SHA-256 hashes formatted for CSP.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CspHashCollectorEmitsStableSha256()
    {
        var styles = new ConcurrentDictionary<byte[], byte>(Common.ByteArrayComparer.Instance);
        var scripts = new ConcurrentDictionary<byte[], byte>(Common.ByteArrayComparer.Instance);
        const string Html = "<style>body{color:red}</style><script>alert(1)</script>";
        CspHashCollector.Collect(Encoding.UTF8.GetBytes(Html), styles, scripts);

        await Assert.That(styles).HasSingleItem();
        await Assert.That(scripts).HasSingleItem();
        await Assert.That(Encoding.UTF8.GetString(styles.Keys.Single())).StartsWith("'sha256-");

        // Idempotent: re-running the same input doesn't grow the set.
        CspHashCollector.Collect(Encoding.UTF8.GetBytes(Html), styles, scripts);
        await Assert.That(styles).HasSingleItem();
    }

    /// <summary>Empty bodies (e.g. <c>&lt;script src="..."&gt;</c>) are skipped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CspHashCollectorSkipsEmptyBodies()
    {
        var styles = new ConcurrentDictionary<byte[], byte>(Common.ByteArrayComparer.Instance);
        var scripts = new ConcurrentDictionary<byte[], byte>(Common.ByteArrayComparer.Instance);
        CspHashCollector.Collect([.. "<script src=\"/x.js\"></script>"u8], styles, scripts);
        await Assert.That(scripts).IsEmpty();
    }

    /// <summary>End-to-end: PrivacyPlugin.Rewrite feeds the CSP collector and FinalizeAsync emits the manifest.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrivacyPluginEmitsCspManifest()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "smkd-csp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(outputRoot);
        try
        {
            var plugin = new PrivacyPlugin(PrivacyOptions.Default with { GenerateCspManifest = true });
            var configure = new BuildConfigureContext("/in", outputRoot, [], new());
            await plugin.ConfigureAsync(configure, CancellationToken.None);

            RunRewrite(plugin, "<style>body{color:red}</style><script>alert(1)</script>"u8);

            var finalize = new BuildFinalizeContext(outputRoot, []);
            await plugin.FinalizeAsync(finalize, CancellationToken.None);

            var manifest = await File.ReadAllTextAsync(Path.Combine(outputRoot, "csp-hashes.json"));
            await Assert.That(manifest).Contains("\"styleSrc\"");
            await Assert.That(manifest).Contains("\"scriptSrc\"");
            await Assert.That(manifest).Contains("sha256-");
        }
        finally
        {
            try
            {
                Directory.Delete(outputRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>Drives one PostRender call against a fresh sink.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    private static void RunRewrite(PrivacyPlugin plugin, ReadOnlySpan<byte> html)
    {
        var output = new ArrayBufferWriter<byte>();
        var ctx = new PagePostRenderContext("page.md", default, html, output);
        plugin.PostRender(in ctx);
    }
}
