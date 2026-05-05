// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Behavior tests for <c>AutorefsRewriter</c>.</summary>
public class AutorefsRewriterTests
{
    /// <summary>Resolved markers get substituted with the registered URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ResolvedMarkersGetRewritten()
    {
        AutorefsRegistry registry = new();
        registry.Register("Foo"u8, [.. "api/foo.html"u8], "Foo"u8);
        var input = "<a href=\"@autoref:Foo\">Foo</a>"u8;

        ArrayBufferWriter<byte> sink = new();
        var changed = AutorefsRewriter.RewriteSpan(input, registry, sink);

        var result = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(changed).IsTrue();
        await Assert.That(result).IsEqualTo("<a href=\"api/foo.html#Foo\">Foo</a>");
    }

    /// <summary>Unresolved markers are preserved verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedMarkersStayVerbatim()
    {
        AutorefsRegistry registry = new();
        var input = "<a href=\"@autoref:Missing\">Missing</a>"u8;

        ArrayBufferWriter<byte> sink = new();
        var changed = AutorefsRewriter.RewriteSpan(input, registry, sink);

        var result = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(changed).IsFalse();
        await Assert.That(result).IsEqualTo("<a href=\"@autoref:Missing\">Missing</a>");
    }

    /// <summary>Multiple markers in one document all get rewritten.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultipleMarkersInOneDocument()
    {
        AutorefsRegistry registry = new();
        registry.Register("a"u8, [.. "a.html"u8], default);
        registry.Register("b"u8, [.. "b.html"u8], "b"u8);
        var input = "see <a href=\"@autoref:a\">A</a> and <a href=\"@autoref:b\">B</a>"u8;

        ArrayBufferWriter<byte> sink = new();
        AutorefsRewriter.RewriteSpan(input, registry, sink);
        var result = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(result).IsEqualTo("see <a href=\"a.html\">A</a> and <a href=\"b.html#b\">B</a>");
    }
}
