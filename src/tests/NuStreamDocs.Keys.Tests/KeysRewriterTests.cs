// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Keys.Tests;

/// <summary>Behaviour tests for <c>KeysRewriter</c>.</summary>
public class KeysRewriterTests
{
    /// <summary>Three-key shortcut emits the full keys span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThreeKeyShortcutRendersKeysSpan()
    {
        const string Expected = "<span class=\"keys\"><kbd class=\"key-ctrl\">Ctrl</kbd><span>+</span><kbd class=\"key-alt\">Alt</kbd><span>+</span><kbd class=\"key-delete\">Delete</kbd></span>";
        await Assert.That(Rewrite("press ++ctrl+alt+del++ to reset"))
            .IsEqualTo($"press {Expected} to reset");
    }

    /// <summary>Single-key shortcut still emits the wrapper span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleKeyShortcutRenders() =>
        await Assert.That(Rewrite("hit ++enter++"))
            .IsEqualTo("hit <span class=\"keys\"><kbd class=\"key-enter\">Enter</kbd></span>");

    /// <summary>Aliases (cmd/command, esc/escape) collapse to the same canonical class.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AliasesNormaliseToCanonicalClass()
    {
        await Assert.That(Rewrite("++command++"))
            .IsEqualTo("<span class=\"keys\"><kbd class=\"key-cmd\">Cmd</kbd></span>");
        await Assert.That(Rewrite("++escape++"))
            .IsEqualTo("<span class=\"keys\"><kbd class=\"key-escape\">Esc</kbd></span>");
    }

    /// <summary>Unknown tokens render literally with a sanitised class.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownTokenRendersLiterally() =>
        await Assert.That(Rewrite("++foo++"))
            .IsEqualTo("<span class=\"keys\"><kbd class=\"key-foo\">foo</kbd></span>");

    /// <summary>Arrows render with their Unicode glyph labels.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ArrowsRenderUnicodeLabels() =>
        await Assert.That(Rewrite("++up++"))
            .IsEqualTo("<span class=\"keys\"><kbd class=\"key-arrow-up\">↑</kbd></span>");

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "Try ++ctrl+x++.\n```\n++ctrl+x++\n```\nAfter ++enter++.";
        const string CtrlX = "<span class=\"keys\"><kbd class=\"key-ctrl\">Ctrl</kbd><span>+</span><kbd class=\"key-x\">x</kbd></span>";
        const string Enter = "<span class=\"keys\"><kbd class=\"key-enter\">Enter</kbd></span>";
        await Assert.That(Rewrite(Source))
            .IsEqualTo($"Try {CtrlX}.\n```\n++ctrl+x++\n```\nAfter {Enter}.");
    }

    /// <summary>Inline code spans pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("use `++ctrl++` then ++ctrl++"))
            .IsEqualTo("use `++ctrl++` then <span class=\"keys\"><kbd class=\"key-ctrl\">Ctrl</kbd></span>");

    /// <summary>Open marker followed by whitespace is not a shortcut.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespaceAfterOpenIsNotAShortcut() => await Assert.That(Rewrite("a++ b ++c")).IsEqualTo("a++ b ++c");

    /// <summary>Plain text round-trips.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "No shortcuts here.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Rewrites <paramref name="input"/> via <c>KeysRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        KeysRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
