// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behavior tests for <c>ExternalLinkPolisher</c>.</summary>
public class ExternalLinkPolisherTests
{
    /// <summary>An external anchor without a <c>rel</c> attribute gains <c>rel="noopener noreferrer"</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddsRelAttributeWhenAbsent()
    {
        var output = Polish(
            "<a href=\"https://example.com\">x</a>",
            PrivacyOptions.Default with { UpgradeMixedContent = false });
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");
    }

    /// <summary>An existing <c>rel</c> attribute keeps its tokens and gains the new ones (no duplicates).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergesIntoExistingRelAttribute()
    {
        var output = Polish(
            "<a href=\"https://example.com\" rel=\"author\">x</a>",
            PrivacyOptions.Default with { UpgradeMixedContent = false });
        await Assert.That(output).Contains("rel=\"author noopener noreferrer\"");
    }

    /// <summary>An anchor that already declares both new tokens is left unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DoesNotDuplicateExistingTokens()
    {
        var output = Polish(
            "<a href=\"https://example.com\" rel=\"noopener noreferrer\">x</a>",
            PrivacyOptions.Default with { UpgradeMixedContent = false });
        var occurrences = output.Split("noopener").Length - 1;
        await Assert.That(occurrences).IsEqualTo(1);
    }

    /// <summary>Internal anchors are left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoresRelativeAnchors()
    {
        const string Source = "<a href=\"/local/page.html\">x</a>";
        var output = Polish(Source, PrivacyOptions.Default with { UpgradeMixedContent = false });
        await Assert.That(output).IsEqualTo(Source);
    }

    /// <summary><c>http://</c> URLs in <c>src</c>/<c>href</c> attributes upgrade to <c>https://</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UpgradesMixedContentInAttributes()
    {
        var output = Polish(
            "<img src=\"http://example.com/a.png\"><a href=\"http://example.com/p\">y</a>",
            PrivacyOptions.Default);
        await Assert.That(output).DoesNotContain("\"http://");
        await Assert.That(output).Contains("\"https://example.com/a.png\"");
        await Assert.That(output).Contains("\"https://example.com/p\"");
    }

    /// <summary><c>target="_blank"</c> is opt-in; off by default.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TargetBlankIsOptIn()
    {
        const string Source = "<a href=\"https://example.com\">x</a>";
        var withoutTarget = Polish(Source, PrivacyOptions.Default with { UpgradeMixedContent = false });
        await Assert.That(withoutTarget).DoesNotContain("target");

        var withTarget = Polish(
            Source,
            PrivacyOptions.Default with { UpgradeMixedContent = false, AddTargetBlank = true });
        await Assert.That(withTarget).Contains("target=\"_blank\"");
    }

    /// <summary>When all rewrite flags are off the polisher returns the input verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NoOpWhenAllFlagsDisabled()
    {
        const string Source = "<a href=\"https://example.com\">x</a>";
        var output = Polish(
            Source,
            PrivacyOptions.Default with
            {
                AddRelNoOpener = false, AddTargetBlank = false, UpgradeMixedContent = false
            });
        await Assert.That(output).IsEqualTo(Source);
    }

    /// <summary>Helper that runs the polisher and returns the string result.</summary>
    /// <param name="source">HTML input.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Polish(string source, in PrivacyOptions options) =>
        Encoding.UTF8.GetString(ExternalLinkPolisher.Polish(Encoding.UTF8.GetBytes(source), options));
}
