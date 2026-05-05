// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Behavior tests for <c>IconShortcodeRewriter</c> in the Material3 theme.</summary>
public class IconShortcodeRewriterTests
{
    /// <summary><c>:material-foo:</c> emits the Material3 variable-font span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MaterialShortcodeRendersSymbolsSpan()
    {
        await Assert.That(Rewrite(":material-home:"))
            .IsEqualTo("<span class=\"material-symbols-outlined\">home</span>");
        await Assert.That(Rewrite(":material-account-circle:"))
            .IsEqualTo("<span class=\"material-symbols-outlined\">account_circle</span>");
    }

    /// <summary><c>:fontawesome-{style}-{name}:</c> emits the correct <c>i</c>-tag (same as Material classic).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FontAwesomeShortcodesRender()
    {
        await Assert.That(Rewrite(":fontawesome-solid-bell:"))
            .IsEqualTo("<i class=\"fa-solid fa-bell\"></i>");
        await Assert.That(Rewrite(":fontawesome-brands-github:"))
            .IsEqualTo("<i class=\"fa-brands fa-github\"></i>");
    }

    /// <summary>Code spans pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CodeSpansArePreserved() =>
        await Assert.That(Rewrite("`:material-home:` and :material-home:"))
            .IsEqualTo("`:material-home:` and <span class=\"material-symbols-outlined\">home</span>");

    /// <summary>Plain text round-trips.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "Plain text content.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Drives bytes through the Material3 rewriter using the Material Symbols class.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        IconShortcodeRewriter.Rewrite(bytes, sink, "material-symbols-outlined"u8);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
