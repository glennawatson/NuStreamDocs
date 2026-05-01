// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Links;

namespace NuStreamDocs.Tests;

/// <summary>Behaviour tests for <c>MarkdownLinkRewriter</c> in the directory-URL mode.</summary>
public class MarkdownLinkRewriterDirectoryUrlsTests
{
    /// <summary>Plain <c>foo.md</c> rewrites to <c>foo/</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleFileBecomesDirectory() =>
        await Assert.That(Rewrite("<a href=\"other.md\">x</a>"))
            .IsEqualTo("<a href=\"other/\">x</a>");

    /// <summary>Sub-path <c>guide/intro.md</c> rewrites to <c>guide/intro/</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubPathBecomesNestedDirectory() =>
        await Assert.That(Rewrite("<a href=\"guide/intro.md\">x</a>"))
            .IsEqualTo("<a href=\"guide/intro/\">x</a>");

    /// <summary><c>index.md</c> resolves to the parent directory's root (no segment).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndexResolvesToParentDirectory()
    {
        await Assert.That(Rewrite("<a href=\"index.md\">x</a>"))
            .IsEqualTo("<a href=\"\">x</a>");
        await Assert.That(Rewrite("<a href=\"guide/index.md\">x</a>"))
            .IsEqualTo("<a href=\"guide/\">x</a>");
    }

    /// <summary>Anchors are preserved across the rewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AnchorPreserved() =>
        await Assert.That(Rewrite("<a href=\"page.md#section\">x</a>"))
            .IsEqualTo("<a href=\"page/#section\">x</a>");

    /// <summary>Anchor on an <c>index.md</c> reference points at the directory root + anchor.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndexWithAnchor() =>
        await Assert.That(Rewrite("<a href=\"guide/index.md#top\">x</a>"))
            .IsEqualTo("<a href=\"guide/#top\">x</a>");

    /// <summary>Absolute URLs ending in <c>.md</c> still pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AbsoluteUrlIsLeftAlone()
    {
        const string Source = "<a href=\"https://example.com/README.md\">x</a>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Drives bytes through the rewriter in directory-URL mode.</summary>
    /// <param name="input">HTML.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string input) =>
        Encoding.UTF8.GetString(MarkdownLinkRewriter.Rewrite(Encoding.UTF8.GetBytes(input), useDirectoryUrls: true));
}
