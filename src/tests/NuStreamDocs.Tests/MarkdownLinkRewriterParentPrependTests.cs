// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Links;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for image / asset URL handling on directory-URL non-index pages.</summary>
public class MarkdownLinkRewriterParentPrependTests
{
    /// <summary>Image src on a non-index dir-URL page gains a <c>../</c> prefix.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImageSrcGetsParentPrependOnNonIndex() =>
        await Assert.That(Rewrite("<img src=\"img/x.png\">", true, true))
            .IsEqualTo("<img src=\"../img/x.png\">");

    /// <summary>Image src on the index page (no prepend) is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImageSrcOnIndexIsUnchanged() =>
        await Assert.That(Rewrite("<img src=\"img/x.png\">", true, false))
            .IsEqualTo("<img src=\"img/x.png\">");

    /// <summary>Absolute https image src is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AbsoluteImageSrcUnchanged()
    {
        const string Source = "<img src=\"https://cdn.example.test/x.png\">";
        await Assert.That(Rewrite(Source, true, true)).IsEqualTo(Source);
    }

    /// <summary>Server-root image src is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RootRelativeImageSrcUnchanged()
    {
        const string Source = "<img src=\"/assets/x.png\">";
        await Assert.That(Rewrite(Source, true, true)).IsEqualTo(Source);
    }

    /// <summary>Already-relative <c>../</c> src is left alone — the author wrote it URL-relative, matching mkdocs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ParentRelativeNonMarkdownUrlIsNotPrepended() =>
        await Assert.That(Rewrite("<img src=\"../already/x.png\">", true, true))
            .IsEqualTo("<img src=\"../already/x.png\">");

    /// <summary>Non-markdown anchor href on a dir-URL non-index page gains a <c>../</c> prefix (lightbox wrapper).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonMarkdownHrefGetsParentPrepend() =>
        await Assert.That(Rewrite("<a href=\"img/x.png\">x</a>", true, true))
            .IsEqualTo("<a href=\"../img/x.png\">x</a>");

    /// <summary>Markdown href continues to rewrite (with parent prepend) alongside the new behavior.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkdownHrefRewritesWithPrepend() =>
        await Assert.That(Rewrite("<a href=\"other.md\">x</a>", true, true))
            .IsEqualTo("<a href=\"../other/\">x</a>");

    /// <summary>A page mixing a markdown href and an image src rewrites both attributes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedHrefAndSrcBothRewrite()
    {
        const string Source = "<a href=\"other.md\">x</a><img src=\"img/y.png\">";
        const string Expected = "<a href=\"../other/\">x</a><img src=\"../img/y.png\">";
        await Assert.That(Rewrite(Source, true, true)).IsEqualTo(Expected);
    }

    /// <summary>Flat-URL mode never prepends, even when the caller asks for it.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FlatUrlModeDoesNotPrepend()
    {
        const string Source = "<img src=\"img/x.png\">";
        await Assert.That(Rewrite(Source, false, true)).IsEqualTo(Source);
    }

    /// <summary>NeedsRewrite returns true for src-only HTML so image-only pages are not skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NeedsRewriteFiresOnSrcOnlyHtml() =>
        await Assert.That(MarkdownLinkRewriter.NeedsRewrite("<img src=\"img/x.png\">"u8)).IsTrue();

    /// <summary>NeedsRewrite returns false for HTML with neither attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NeedsRewriteFalseForPlainHtml() =>
        await Assert.That(MarkdownLinkRewriter.NeedsRewrite("<p>plain</p>"u8)).IsFalse();

    /// <summary>Drives bytes through the rewriter with the requested toggles.</summary>
    /// <param name="input">HTML.</param>
    /// <param name="useDirectoryUrls">Directory-URL mode toggle.</param>
    /// <param name="prependParent">Prepend-parent toggle.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string input, bool useDirectoryUrls, bool prependParent)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(bytes.Length + 16);
        MarkdownLinkRewriter.RewriteInto(bytes, useDirectoryUrls, prependParent, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
