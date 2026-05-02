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
        var matcher = new UrlPatternMatcher(["https://fonts.googleapis.com/css*"]);
        await Assert.That(matcher.IsMatch("https://fonts.googleapis.com/css")).IsTrue();
        await Assert.That(matcher.IsMatch("https://fonts.googleapis.com/css?family=Roboto")).IsTrue();
        await Assert.That(matcher.IsMatch("https://fonts.googleapis.com/recaptcha/foo")).IsFalse();
    }

    /// <summary>An include pattern broadens the allow set even when the host isn't on <c>HostsAllowed</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IncludePatternBroadensAllowSet()
    {
        var filter = new HostFilter(
            hostsToSkip: null,
            hostsAllowed: ["allowed.example"],
            includePatterns: ["https://other.example/css*"],
            excludePatterns: null);
        await Assert.That(filter.ShouldLocalize("https://allowed.example/x.png")).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://other.example/css/main.css")).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://other.example/recaptcha/x")).IsFalse();
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
            excludePatterns: ["https://*.googleapis.com/recaptcha/*"]);
        await Assert.That(filter.ShouldLocalize("https://fonts.googleapis.com/css")).IsTrue();
        await Assert.That(filter.ShouldLocalize("https://fonts.googleapis.com/recaptcha/x")).IsFalse();
    }

    /// <summary>Inline <c>&lt;style&gt;</c> bodies produce stable SHA-256 hashes formatted for CSP.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CspHashCollectorEmitsStableSha256()
    {
        var styles = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var scripts = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        const string Html = "<style>body{color:red}</style><script>alert(1)</script>";
        CspHashCollector.Collect(Encoding.UTF8.GetBytes(Html), styles, scripts);

        await Assert.That(styles).HasSingleItem();
        await Assert.That(scripts).HasSingleItem();
        await Assert.That(styles.Keys.Single()).StartsWith("'sha256-");

        // Idempotent: re-running the same input doesn't grow the set.
        CspHashCollector.Collect(Encoding.UTF8.GetBytes(Html), styles, scripts);
        await Assert.That(styles).HasSingleItem();
    }

    /// <summary>Empty bodies (e.g. <c>&lt;script src="..."&gt;</c>) are skipped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CspHashCollectorSkipsEmptyBodies()
    {
        var styles = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var scripts = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        CspHashCollector.Collect([.. "<script src=\"/x.js\"></script>"u8], styles, scripts);
        await Assert.That(scripts).IsEmpty();
    }

    /// <summary>End-to-end: PrivacyPlugin.OnRenderPage feeds the CSP collector and OnFinalize emits the manifest.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrivacyPluginEmitsCspManifest()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "smkd-csp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(outputRoot);
        try
        {
            var plugin = new PrivacyPlugin(PrivacyOptions.Default with { GenerateCspManifest = true });
            var configure = new PluginConfigureContext(default, "/in", outputRoot, []);
            await plugin.OnConfigureAsync(configure, CancellationToken.None);

            byte[] html = [.. "<style>body{color:red}</style><script>alert(1)</script>"u8];
            var sink = new ArrayBufferWriter<byte>();
            sink.Write(html);
            var render = new PluginRenderContext("page.md", html, sink);
            await plugin.OnRenderPageAsync(render, CancellationToken.None);

            var finalize = new PluginFinalizeContext(outputRoot);
            await plugin.OnFinalizeAsync(finalize, CancellationToken.None);

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
}
