// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Emoji.Tests;

/// <summary>Lifecycle / registration tests for <c>EmojiPlugin</c>.</summary>
public class EmojiPluginTests
{
    /// <summary>The preprocessor produces the same output as the rewriter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessMatchesRewriter()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        new EmojiPlugin().Preprocess("hi :rocket:"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan))
            .IsEqualTo("hi <span class=\"twemoji\">🚀</span>");
    }

    /// <summary>The plugin advertises a stable name.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new EmojiPlugin().Name).IsEqualTo("emoji");

    /// <summary>Preprocess with a null sink throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessRejectsNullSink()
    {
        var plugin = new EmojiPlugin();
        var ex = Assert.Throws<ArgumentNullException>(() => plugin.Preprocess(default, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseEmoji registers the plugin and returns the builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseEmojiRegisters()
    {
        var builder = new DocBuilder();
        var returned = builder.UseEmoji();
        await Assert.That(returned).IsSameReferenceAs(builder);
    }

    /// <summary>UseEmoji guards against a null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseEmojiRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderEmojiExtensions.UseEmoji(null!));
        await Assert.That(ex).IsNotNull();
    }
}
