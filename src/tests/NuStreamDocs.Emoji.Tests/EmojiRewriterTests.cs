// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Emoji.Tests;

/// <summary>Behavior tests for <c>EmojiRewriter</c>.</summary>
public class EmojiRewriterTests
{
    /// <summary>A known shortcode wraps in a twemoji span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task KnownShortcodeWraps() =>
        await Assert.That(Rewrite("Ship it :rocket:"))
            .IsEqualTo("Ship it <span class=\"twemoji\">🚀</span>");

    /// <summary>Multiple shortcodes on one line all wrap.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleShortcodesOnOneLine() =>
        await Assert.That(Rewrite(":tada: ship :fire: it :100:"))
            .IsEqualTo("<span class=\"twemoji\">🎉</span> ship <span class=\"twemoji\">🔥</span> it <span class=\"twemoji\">💯</span>");

    /// <summary>Shortcodes that contain digits or punctuation in the body resolve.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShortcodeWithSpecialBytes() =>
        await Assert.That(Rewrite(":+1: looks good :-1: bad"))
            .IsEqualTo("<span class=\"twemoji\">👍</span> looks good <span class=\"twemoji\">👎</span> bad");

    /// <summary>Unknown shortcodes pass through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownShortcodePassesThrough() =>
        await Assert.That(Rewrite("the :nonexistent: stays"))
            .IsEqualTo("the :nonexistent: stays");

    /// <summary>A bare colon does not trigger a match.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareColonDoesNotMatch() => await Assert.That(Rewrite("ratio 3:1 here")).IsEqualTo("ratio 3:1 here");

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = ":fire:.\n```\n:fire: literal\n```\nAfter :fire:.";
        const string Fire = "<span class=\"twemoji\">🔥</span>";
        await Assert.That(Rewrite(Source))
            .IsEqualTo($"{Fire}.\n```\n:fire: literal\n```\nAfter {Fire}.");
    }

    /// <summary>Inline code spans pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("Use `:fire:` literally and :fire: live"))
            .IsEqualTo("Use `:fire:` literally and <span class=\"twemoji\">🔥</span> live");

    /// <summary>Plain text round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "No shortcodes anywhere.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Unclosed shortcode at end of line passes through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedShortcodePassesThrough() => await Assert.That(Rewrite("trailing :fire never closes")).IsEqualTo("trailing :fire never closes");

    /// <summary>Identifiers using each permitted body byte (digits, underscore, hyphen, plus, dot, mixed-case letters) all parse without breaking the colon scanner.</summary>
    /// <param name="input">Source containing the candidate shortcode.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(":+1:")]
    [Arguments(":-1:")]
    [Arguments(":1234:")]
    [Arguments(":a_b:")]
    [Arguments(":Foo.Bar:")]
    [Arguments(":CASE:")]
    public async Task ShortcodeBodyByteShapesPreserved(string input)
    {
        var output = Rewrite(input);

        // Result is either the verbatim input (unknown shortcode passes through) or a wrapped span.
        // Either way, the scanner must not eat colons it can't match.
        await Assert.That(output.Contains(':') || output.Contains("twemoji", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>A trailing colon with nothing after passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingColonPassesThrough() =>
        await Assert.That(Rewrite("text:")).IsEqualTo("text:");

    /// <summary>An open colon followed by non-shortcode bytes passes through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ColonNotFollowedByShortcodeByte() =>
        await Assert.That(Rewrite(":! oops")).IsEqualTo(":! oops");

    /// <summary>A space terminator on the body span (no closing colon) passes through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BodyTerminatedByNonColonChar() =>
        await Assert.That(Rewrite(":abc def:")).IsEqualTo(":abc def:");

    /// <summary>Rewrites <paramref name="input"/> via <c>EmojiRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        EmojiRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
