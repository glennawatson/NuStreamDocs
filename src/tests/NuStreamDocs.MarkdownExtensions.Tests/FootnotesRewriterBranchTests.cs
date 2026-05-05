// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Footnotes;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Branch-coverage edge cases for FootnotesRewriter.</summary>
public class FootnotesRewriterBranchTests
{
    /// <summary>Multiple references to the same definition share the footnote id.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepeatedReferences()
    {
        var output = Rewrite("First[^a] then[^a].\n\n[^a]: note\n");
        var occurrences = output.Split("href=\"#fn-a\"").Length - 1;
        await Assert.That(occurrences).IsGreaterThanOrEqualTo(2);
    }

    /// <summary>Bracket without caret is not a footnote reference.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BracketWithoutCaret() =>
        await Assert.That(Rewrite("not [a] note\n")).IsEqualTo("not [a] note\n");

    /// <summary>Empty input passes through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Reference at the end of input is rewritten.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReferenceAtEnd()
    {
        var output = Rewrite("trailing[^z]\n\n[^z]: note\n");
        await Assert.That(output).Contains("href=\"#fn-z\"");
    }

    /// <summary>Numeric ids are accepted and rendered.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NumericIds()
    {
        var output = Rewrite("a[^1] b[^2].\n\n[^1]: one\n[^2]: two\n");
        await Assert.That(output).Contains("fn-1");
        await Assert.That(output).Contains("fn-2");
    }

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        FootnotesRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
