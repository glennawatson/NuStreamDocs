// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Snippets.Tests;

/// <summary>Direct tests for SnippetsByteWriter.</summary>
public class SnippetsRewriterCopyByteTests
{
    /// <summary>WriteOne writes the byte once.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WritesSingleByte()
    {
        ArrayBufferWriter<byte> sink = new(2);
        SnippetsByteWriter.WriteOne(sink, (byte)'X');
        await Assert.That(sink.WrittenCount).IsEqualTo(1);
        await Assert.That(sink.WrittenSpan[0]).IsEqualTo((byte)'X');
    }
}
