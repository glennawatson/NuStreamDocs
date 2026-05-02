// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Footnotes;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Parameterized id / definition shape tests for FootnotesRewriter.</summary>
public class FootnotesRewriterParameterizedTests
{
    /// <summary>Each id shape (ascii, digit, mixed, hyphen, underscore) round-trips through the rewriter.</summary>
    /// <param name="id">Footnote identifier.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("a")]
    [Arguments("1")]
    [Arguments("note42")]
    [Arguments("with-hyphen")]
    [Arguments("with_underscore")]
    [Arguments("MixedCase")]
    public async Task IdShapesRoundTrip(string id)
    {
        var output = Rewrite($"see[^{id}].\n\n[^{id}]: definition\n");
        await Assert.That(output).Contains($"href=\"#fn-{id}\"");
        await Assert.That(output).Contains($"id=\"fn-{id}\"");
    }

    /// <summary>Definition body styles render through inline markdown.</summary>
    /// <param name="body">Body of the definition.</param>
    /// <param name="expectedFragment">Fragment expected in the rendered footnote section.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("**bold**", "<strong>bold</strong>")]
    [Arguments("*em*", "<em>em</em>")]
    [Arguments("`code`", "<code>code</code>")]
    [Arguments("plain", "plain")]
    public async Task DefinitionBodyMarkdown(string body, string expectedFragment)
    {
        var output = Rewrite($"x[^a].\n\n[^a]: {body}\n");
        await Assert.That(output).Contains(expectedFragment);
    }

    /// <summary>Reference at various positions in the line all rewrite.</summary>
    /// <param name="placement">Wrapper text containing the reference.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("[^x]")]
    [Arguments("a[^x]")]
    [Arguments("[^x]b")]
    [Arguments("[^x][^x]")]
    [Arguments("[^x] and [^x]")]
    public async Task ReferencePlacements(string placement)
    {
        var output = Rewrite($"{placement}\n\n[^x]: note\n");
        await Assert.That(output).Contains("href=\"#fn-x\"");
    }

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        FootnotesRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
