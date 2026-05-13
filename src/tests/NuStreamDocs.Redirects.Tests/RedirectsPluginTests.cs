// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Redirects.Tests;

/// <summary>End-to-end coverage for <see cref="RedirectsPlugin"/> (drives configure → scan → finalize directly).</summary>
public class RedirectsPluginTests
{
    /// <summary>Config and frontmatter redirects both land in <c>_redirects</c>; a meta-refresh page is written for each; <c>_headers</c> has the default cache rules.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitsRedirectsHeadersAndMetaPages()
    {
        using TempDir dir = new();
        var plugin = new RedirectsPlugin(RedirectsOptions.Default.Add("/old/section/"u8, "/new/section/"u8));
        await plugin.ConfigureAsync(
            new(dir.Root, dir.Root, [], new()) { UseDirectoryUrls = true },
            CancellationToken.None);
        ScanPage(plugin, "guide/intro.md", "---\nredirect_from: /legacy-intro/\n---\nbody"u8);
        ScanPage(plugin, "ref/api.md", "---\nredirect_from:\n  - /old-api/\n  - /older-api/\n---\nbody"u8);
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);

        var redirects = await File.ReadAllTextAsync(Path.Combine(dir.Root, "_redirects"));
        await Assert.That(redirects).Contains("/old/section/  /new/section/  301");
        await Assert.That(redirects).Contains("/legacy-intro/  /guide/intro/  301");
        await Assert.That(redirects).Contains("/old-api/  /ref/api/  301");
        await Assert.That(redirects).Contains("/older-api/  /ref/api/  301");

        var headers = await File.ReadAllTextAsync(Path.Combine(dir.Root, "_headers"));
        await Assert.That(headers).Contains("/assets/fonts/*");
        await Assert.That(headers).Contains("immutable");

        var legacyPage = await File.ReadAllTextAsync(Path.Combine(dir.Root, "legacy-intro", "index.html"));
        await Assert.That(legacyPage).Contains("<meta http-equiv=\"refresh\" content=\"0; url=/guide/intro/\">");
    }

    /// <summary>A redirect whose source is already a rendered page keeps the <c>_redirects</c> entry but doesn't overwrite the page.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DoesNotClobberAnExistingPage()
    {
        using TempDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Root, "existing"));
        await File.WriteAllTextAsync(Path.Combine(dir.Root, "existing", "index.html"), "<html>real page</html>");

        var plugin = new RedirectsPlugin(RedirectsOptions.Default.Add("/existing/"u8, "/new/"u8));
        await plugin.ConfigureAsync(
            new(dir.Root, dir.Root, [], new()) { UseDirectoryUrls = true },
            CancellationToken.None);
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);

        await Assert.That(await File.ReadAllTextAsync(Path.Combine(dir.Root, "existing", "index.html")))
            .IsEqualTo("<html>real page</html>");
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(dir.Root, "_redirects")))
            .Contains("/existing/  /new/  301");
    }

    /// <summary>With everything disabled, no files are written.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NothingWhenDisabled()
    {
        using TempDir dir = new();
        var options = RedirectsOptions.Default
            .Add("/old/"u8, "/new/"u8)
            .WithoutRedirectsFile()
            .WithoutHeadersFile()
            .WithoutMetaRefreshPages()
            .WithoutDefaultCacheHeaders();
        var plugin = new RedirectsPlugin(options);
        await plugin.ConfigureAsync(new(dir.Root, dir.Root, [], new()), CancellationToken.None);
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "_redirects"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(dir.Root, "_headers"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(dir.Root, "old", "index.html"))).IsFalse();
        await Assert.That(plugin.Name.SequenceEqual("redirects"u8)).IsTrue();
    }

    /// <summary>Drives one Scan call against the plugin.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="relativePath">Source-relative markdown path.</param>
    /// <param name="source">Markdown bytes (frontmatter + body).</param>
    private static void ScanPage(RedirectsPlugin plugin, string relativePath, ReadOnlySpan<byte> source)
    {
        PageScanContext ctx = new(relativePath, source, "<html></html>"u8);
        plugin.Scan(in ctx);
    }
}
