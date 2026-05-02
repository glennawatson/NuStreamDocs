// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Optimize.Tests;

/// <summary>Direct tests for the snapshot-then-rewrite helper.</summary>
public class HtmlSnapshotRewriterTests
{
    /// <summary>Empty buffer is a no-op; the rewrite callback never runs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyBufferNoOp()
    {
        var writer = new ArrayBufferWriter<byte>();
        var invocations = 0;
        HtmlSnapshotRewriter.Rewrite(writer, 0, (_, _, _) => invocations++);
        await Assert.That(invocations).IsEqualTo(0);
        await Assert.That(writer.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Snapshot reflects the original bytes; the writer is reset before the callback runs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SnapshotMirrorsBufferAndWriterReset()
    {
        var writer = new ArrayBufferWriter<byte>();
        WriteUtf8(writer, "hello");
        var observedSnapshot = string.Empty;
        var observedWriterCount = -1;
        HtmlSnapshotRewriter.Rewrite(writer, 0, (snapshot, w, _) =>
        {
            observedSnapshot = Encoding.UTF8.GetString(snapshot);
            observedWriterCount = w.WrittenCount;
        });
        await Assert.That(observedSnapshot).IsEqualTo("hello");
        await Assert.That(observedWriterCount).IsEqualTo(0);
    }

    /// <summary>Bytes written by the callback land in the original writer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteOutputLandsInWriter()
    {
        var writer = new ArrayBufferWriter<byte>();
        WriteUtf8(writer, "before");
        HtmlSnapshotRewriter.Rewrite(writer, "after", static (_, w, replacement) => WriteUtf8(w, replacement));
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("after");
    }

    /// <summary>State value reaches the callback unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StateForwardedToCallback()
    {
        var writer = new ArrayBufferWriter<byte>();
        WriteUtf8(writer, "x");
        var observed = 0;
        HtmlSnapshotRewriter.Rewrite(writer, 42, (_, _, state) => observed = state);
        await Assert.That(observed).IsEqualTo(42);
    }

    /// <summary>Null writer throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullWriterThrows() =>
        await Assert.That(() => HtmlSnapshotRewriter.Rewrite(null!, 0, (_, _, _) => { })).Throws<ArgumentNullException>();

    /// <summary>Null callback throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullRewriteThrows()
    {
        var writer = new ArrayBufferWriter<byte>();
        WriteUtf8(writer, "x");
        await Assert.That(() => HtmlSnapshotRewriter.Rewrite(writer, 0, null!)).Throws<ArgumentNullException>();
    }

    /// <summary>UTF-8 encodes <paramref name="value"/> directly into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="value">Source text.</param>
    private static void WriteUtf8(ArrayBufferWriter<byte> writer, string value)
    {
        var dst = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length));
        var n = Encoding.UTF8.GetBytes(value, dst);
        writer.Advance(n);
    }
}
