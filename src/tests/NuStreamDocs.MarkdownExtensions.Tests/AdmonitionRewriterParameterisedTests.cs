// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Admonitions;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Parameterised type-token coverage for AdmonitionRewriter.</summary>
public class AdmonitionRewriterParameterisedTests
{
    /// <summary>Each recognised admonition type produces a corresponding class on the wrapper.</summary>
    /// <param name="type">Admonition type token.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("note")]
    [Arguments("tip")]
    [Arguments("hint")]
    [Arguments("warning")]
    [Arguments("caution")]
    [Arguments("danger")]
    [Arguments("error")]
    [Arguments("info")]
    [Arguments("success")]
    [Arguments("quote")]
    [Arguments("custom-type")]
    public async Task TypeTokenRoundTrips(string type)
    {
        var output = Rewrite($"!!! {type}\n    body line\n");
        await Assert.That(output).Contains($"class=\"admonition {type}\"");
    }

    /// <summary>Optional title is rendered into a header element.</summary>
    /// <param name="title">Quoted title.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("Title")]
    [Arguments("Multi word title")]
    [Arguments("with-symbol!")]
    public async Task OptionalTitleRendered(string title)
    {
        var output = Rewrite($"!!! note \"{title}\"\n    body\n");
        await Assert.That(output).Contains(title);
    }

    /// <summary>Block without a body renders as an empty wrapper.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyBody()
    {
        var output = Rewrite("!!! note\n");
        await Assert.That(output).Contains("admonition note");
    }

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        AdmonitionRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
