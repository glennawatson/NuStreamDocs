// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Metadata.Tests;

/// <summary>Branch-coverage edge cases for FrontmatterSplicer.</summary>
public class FrontmatterSplicerBranchTests
{
    /// <summary>Empty extra makes Splice a passthrough.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyExtraPassThrough()
    {
        var sink = new ArrayBufferWriter<byte>();
        FrontmatterSplicer.Splice("# body"u8, [], sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("# body");
    }

    /// <summary>Body with no frontmatter wraps the inherited keys with <c>---</c> fences.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoFrontmatterWraps()
    {
        var sink = new ArrayBufferWriter<byte>();
        FrontmatterSplicer.Splice("# body\n"u8, "title: A\n"u8, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).StartsWith("---\ntitle: A\n---\n");
        await Assert.That(output).Contains("# body");
    }

    /// <summary>Inherited extra without trailing newline still emits a newline before the closing fence.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoTrailingNewlineNormalised()
    {
        var sink = new ArrayBufferWriter<byte>();
        FrontmatterSplicer.Splice("body"u8, "title: A"u8, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("title: A\n---\n");
    }

    /// <summary>Existing frontmatter wins for already-declared keys; new keys are appended.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExistingKeysWin()
    {
        var sink = new ArrayBufferWriter<byte>();
        FrontmatterSplicer.Splice(
            "---\ntitle: Page\n---\nbody"u8,
            "title: Inherited\nauthor: Inh\n"u8,
            sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("title: Page");
        await Assert.That(output).DoesNotContain("title: Inherited");
        await Assert.That(output).Contains("author: Inh");
    }

    /// <summary>Body that opens with <c>---</c> but has no closing fence is treated as no frontmatter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedOpenerTreatedAsNone()
    {
        var sink = new ArrayBufferWriter<byte>();
        FrontmatterSplicer.Splice(
            "---\ntitle: Open\nstill body\n"u8,
            "author: A\n"u8,
            sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("---\nauthor: A\n---\n");
    }

    /// <summary>A bare <c>---</c> with no newline is not a frontmatter opener.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DashesWithoutNewlineIgnored()
    {
        var sink = new ArrayBufferWriter<byte>();
        FrontmatterSplicer.Splice("---"u8, "k: v\n"u8, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).StartsWith("---\nk: v\n---\n");
    }

    /// <summary>CRLF frontmatter is recognised.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CrlfFrontmatter()
    {
        var sink = new ArrayBufferWriter<byte>();
        FrontmatterSplicer.Splice(
            "---\r\ntitle: Crlf\r\n---\r\nbody"u8,
            "author: A\n"u8,
            sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("title: Crlf");
        await Assert.That(output).Contains("author: A");
    }
}
