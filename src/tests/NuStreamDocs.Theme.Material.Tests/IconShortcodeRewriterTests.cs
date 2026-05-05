// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Behavior tests for <c>IconShortcodeRewriter</c> in the Material classic theme.</summary>
public class IconShortcodeRewriterTests
{
    /// <summary><c>:material-foo:</c> emits a Material classic icon-font span (with hyphens normalized to underscores for ligatures).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MaterialShortcodeRendersIconFontSpan()
    {
        await Assert.That(Rewrite("Click :material-home: now"))
            .IsEqualTo("Click <span class=\"material-icons\">home</span> now");
        await Assert.That(Rewrite(":material-account-circle:"))
            .IsEqualTo("<span class=\"material-icons\">account_circle</span>");
    }

    /// <summary><c>:fontawesome-{style}-{name}:</c> emits the correct <c>i</c>-tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FontAwesomeShortcodesRenderTags()
    {
        await Assert.That(Rewrite(":fontawesome-solid-bell:"))
            .IsEqualTo("<i class=\"fa-solid fa-bell\"></i>");
        await Assert.That(Rewrite(":fontawesome-brands-github:"))
            .IsEqualTo("<i class=\"fa-brands fa-github\"></i>");
        await Assert.That(Rewrite(":fontawesome-regular-user:"))
            .IsEqualTo("<i class=\"fa-regular fa-user\"></i>");
    }

    /// <summary>Unknown styles fall through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownFontAwesomeStyleFallsThrough() => await Assert.That(Rewrite(":fontawesome-magic-bell:")).IsEqualTo(":fontawesome-magic-bell:");

    /// <summary>Plain emoji-style shortcodes (no <c>material-</c> / <c>fontawesome-</c> prefix) pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnrelatedShortcodesPassThrough() => await Assert.That(Rewrite("plain :smile: text")).IsEqualTo("plain :smile: text");

    /// <summary>Fenced and inline code spans pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CodeSpansArePreserved()
    {
        await Assert.That(Rewrite("```\n:material-home:\n```\n:material-home:"))
            .IsEqualTo("```\n:material-home:\n```\n<span class=\"material-icons\">home</span>");
        await Assert.That(Rewrite("Use `:material-home:` literally and :material-home: live"))
            .IsEqualTo("Use `:material-home:` literally and <span class=\"material-icons\">home</span> live");
    }

    /// <summary>Plain text round-trips.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "No icons in here.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Drives bytes through the Material rewriter using the Material classic class.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        IconShortcodeRewriter.Rewrite(bytes, sink, "material-icons"u8);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
