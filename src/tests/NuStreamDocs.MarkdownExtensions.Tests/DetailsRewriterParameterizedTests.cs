// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Details;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Parameterized collapse / type-token coverage for DetailsRewriter.</summary>
public class DetailsRewriterParameterizedTests
{
    /// <summary><c>???+</c> opener emits the <c>open</c> attribute; <c>???</c> doesn't.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClosedOpenerHasNoOpenAttribute() =>
        await Assert.That(Rewrite("??? note\n    body\n")).DoesNotContain(" open>");

    /// <summary><c>???+</c> opener emits the <c>open</c> attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExpandedOpenerHasOpenAttribute() =>
        await Assert.That(Rewrite("???+ note\n    body\n")).Contains(" open>");

    /// <summary>Each recognized type token becomes a class on the wrapper.</summary>
    /// <param name="type">Type token.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("note")]
    [Arguments("tip")]
    [Arguments("warning")]
    [Arguments("danger")]
    [Arguments("info")]
    [Arguments("success")]
    [Arguments("quote")]
    public async Task TypeTokens(string type)
    {
        var output = Rewrite($"??? {type}\n    body\n");
        await Assert.That(output).Contains($"class=\"{type}\"");
    }

    /// <summary>Quoted summary becomes a &lt;summary&gt;.</summary>
    /// <param name="summary">Quoted title.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("Click me")]
    [Arguments("Multi word")]
    [Arguments("with-symbol!")]
    public async Task SummaryRendered(string summary)
    {
        var output = Rewrite($"??? note \"{summary}\"\n    body\n");
        await Assert.That(output).Contains($"<summary>{summary}</summary>");
    }

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        DetailsRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
