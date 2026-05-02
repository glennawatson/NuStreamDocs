// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>FrontmatterValueExtractor</c>.</summary>
public class FrontmatterValueExtractorTests
{
    /// <summary>An inline scalar value is appended preceded by a space.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineScalarAppended() =>
        await Assert.That(Extract("---\ntitle: Hello\n---\nbody", "title"))
            .IsEqualTo(" Hello");

    /// <summary>A block-list value appends each entry separated by spaces.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockListAppended() =>
        await Assert.That(Extract("---\ntags:\n  - alpha\n  - beta\n---\nbody", "tags"))
            .IsEqualTo(" alpha beta");

    /// <summary>A trailing inline scalar plus a continuation line both fold into the output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScalarPlusContinuationAppended() =>
        await Assert.That(Extract("---\nsummary: Lead\n  more\n---\nbody", "summary"))
            .IsEqualTo(" Lead more");

    /// <summary>An unrecognized key is silently skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownKeyIsSilent() =>
        await Assert.That(Extract("---\ntitle: Hello\n---\nbody", "missing"))
            .IsEqualTo(string.Empty);

    /// <summary>An empty key array is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyKeyArrayIsNoOp()
    {
        var sink = new ArrayBufferWriter<byte>(16);
        FrontmatterValueExtractor.AppendKeysTo("---\ntitle: A\n---\nbody"u8, [], sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Empty key strings are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyKeyStringSkipped() =>
        await Assert.That(Extract("---\ntitle: A\n---\nbody", string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Source without a frontmatter delimiter is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoFrontmatterIsNoOp() =>
        await Assert.That(Extract("just body, no frontmatter", "title")).IsEqualTo(string.Empty);

    /// <summary>An open-ended frontmatter (no closing <c>---</c>) is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedFrontmatterIsNoOp() =>
        await Assert.That(Extract("---\ntitle: A\nbody (no close)", "title")).IsEqualTo(string.Empty);

    /// <summary>An empty inline value is silently dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInlineValueIsDropped() =>
        await Assert.That(Extract("---\nempty:\n---\nbody", "empty")).IsEqualTo(string.Empty);

    /// <summary>The argument-null guard fires on a null sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullSinkRejected()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            FrontmatterValueExtractor.AppendKeysTo("---\ntitle: A\n---\n"u8, ["title"], null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Multiple keys yield concatenated values in registration order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleKeysAppendedInOrder()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        FrontmatterValueExtractor.AppendKeysTo(
            "---\ntitle: A\nauthor: B\n---\nbody"u8,
            ["title", "author"],
            sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(" A B");
    }

    /// <summary>Drives the extractor and returns the captured UTF-8 bytes as a UTF-16 string.</summary>
    /// <param name="source">Markdown source text.</param>
    /// <param name="key">Frontmatter key.</param>
    /// <returns>The collected text.</returns>
    private static string Extract(string source, string key)
    {
        var sink = new ArrayBufferWriter<byte>(64);
        FrontmatterValueExtractor.AppendKeysTo(Encoding.UTF8.GetBytes(source), [key], sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
