// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>End-to-end tests for the corpus + internal validator.</summary>
public class InternalLinkValidatorTests
{
    /// <summary>A clean site produces no diagnostics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CleanSiteProducesNoDiagnostics()
    {
        var dir = TempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "index.html"),
                "<h1 id=\"top\">Hello</h1><a href=\"about.html\">About</a>");
            await File.WriteAllTextAsync(
                Path.Combine(dir, "about.html"),
                "<h1 id=\"about\">About</h1><a href=\"index.html#top\">Home</a>");

            var corpus = await ValidationCorpus.BuildAsync(dir, parallelism: 4, CancellationToken.None);
            var diags = await InternalLinkValidator.ValidateAsync(corpus, parallelism: 4, CancellationToken.None);

            await Assert.That(diags.Length).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A link to a missing page raises an error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BrokenInternalLinkIsReported()
    {
        var dir = TempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "index.html"),
                "<h1 id=\"top\">Hi</h1><a href=\"missing.html\">missing</a>");

            var corpus = await ValidationCorpus.BuildAsync(dir, 4, CancellationToken.None);
            var diags = await InternalLinkValidator.ValidateAsync(corpus, 4, CancellationToken.None);

            await Assert.That(diags.Length).IsEqualTo(1);
            await Assert.That(diags[0].Severity).IsEqualTo(LinkSeverity.Error);
            await Assert.That(diags[0].Link).IsEqualTo("missing.html");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A page-local anchor that doesn't match any heading is reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BrokenSamePageAnchorIsReported()
    {
        var dir = TempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "index.html"),
                "<h1 id=\"top\">Hi</h1><a href=\"#nope\">missing-anchor</a>");

            var corpus = await ValidationCorpus.BuildAsync(dir, 4, CancellationToken.None);
            var diags = await InternalLinkValidator.ValidateAsync(corpus, 4, CancellationToken.None);

            await Assert.That(diags.Length).IsEqualTo(1);
            await Assert.That(diags[0].Link).IsEqualTo("#nope");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A cross-page anchor that doesn't match a target heading is reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BrokenCrossPageAnchorIsReported()
    {
        var dir = TempDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "index.html"),
                "<h1 id=\"top\">Hi</h1><a href=\"about.html#missing\">x</a>");
            await File.WriteAllTextAsync(
                Path.Combine(dir, "about.html"),
                "<h1 id=\"about\">About</h1>");

            var corpus = await ValidationCorpus.BuildAsync(dir, 4, CancellationToken.None);
            var diags = await InternalLinkValidator.ValidateAsync(corpus, 4, CancellationToken.None);

            await Assert.That(diags.Length).IsEqualTo(1);
            await Assert.That(diags[0].Link).IsEqualTo("about.html#missing");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Relative <c>../</c> traversal resolves correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RelativeTraversalResolves()
    {
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "guide"));
            await File.WriteAllTextAsync(
                Path.Combine(dir, "guide", "intro.html"),
                "<a href=\"../index.html\">home</a>");
            await File.WriteAllTextAsync(
                Path.Combine(dir, "index.html"),
                "<h1 id=\"top\">Home</h1>");

            var corpus = await ValidationCorpus.BuildAsync(dir, 4, CancellationToken.None);
            var diags = await InternalLinkValidator.ValidateAsync(corpus, 4, CancellationToken.None);

            await Assert.That(diags.Length).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Builds a unique temp directory for one test.</summary>
    /// <returns>Absolute path.</returns>
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smd-linkval-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
