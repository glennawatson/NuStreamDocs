// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Behaviour tests for the docfx cross-reference preprocessor.</summary>
public class AutorefsReferenceLinkPreprocessorTests
{
    /// <summary>A simple <c>[Foo][T:Bar]</c> rewrites to the autoref-marker URL shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesTypeReference()
    {
        var output = Rewrite("Implements: [IFilesystemProvider][T:Akavache.IFilesystemProvider]\n");
        await Assert.That(output).IsEqualTo(
            "Implements: [IFilesystemProvider](@autoref:T:Akavache.IFilesystemProvider)\n");
    }

    /// <summary>Method commentIds (with parens + arg list) round-trip.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesMethodReferenceWithArgs()
    {
        var output = Rewrite("Calls [Bar][M:Foo.Bar(System.Int32)] often.\n");
        await Assert.That(output).IsEqualTo(
            "Calls [Bar](@autoref:M:Foo.Bar(System.Int32)) often.\n");
    }

    /// <summary>Field/property/event/namespace commentIds also rewrite.</summary>
    /// <param name="prefix">Prefix letter.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("F")]
    [Arguments("P")]
    [Arguments("E")]
    [Arguments("N")]
    public async Task RewritesAllCommentIdPrefixes(string prefix)
    {
        var src = $"see [Doohickey][{prefix}:Foo.Bar]\n";
        var output = Rewrite(src);
        await Assert.That(output).IsEqualTo(
            $"see [Doohickey](@autoref:{prefix}:Foo.Bar)\n");
    }

    /// <summary>Reference shapes that don't carry a commentId prefix are left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LeavesNonCrefReferenceLinksAlone()
    {
        const string Src = "see [Foo][bar] for more\n[bar]: https://example.test\n";
        var output = Rewrite(Src);
        await Assert.That(output).IsEqualTo(Src);
    }

    /// <summary>Inline links <c>[text](url)</c> already-resolved are not double-rewritten.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LeavesInlineLinksAlone()
    {
        const string Src = "see [Foo](https://example.test/T:Bar)\n";
        var output = Rewrite(Src);
        await Assert.That(output).IsEqualTo(Src);
    }

    /// <summary>Cref-shaped tokens inside an inline code span pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipsInlineCodeSpan()
    {
        const string Src = "the literal `[IFilesystemProvider][T:Akavache.IFilesystemProvider]` shape\n";
        var output = Rewrite(Src);
        await Assert.That(output).IsEqualTo(Src);
    }

    /// <summary>Cref-shaped tokens inside a fenced code block pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipsFencedCodeBlock()
    {
        var src = string.Join(
            "\n",
            "```",
            "var x = [IFoo][T:N.IFoo];",
            "```",
            "but [Foo][T:N.Foo] elsewhere\n");
        var output = Rewrite(src);
        await Assert.That(output).Contains("var x = [IFoo][T:N.IFoo];");
        await Assert.That(output).Contains("[Foo](@autoref:T:N.Foo)");
    }

    /// <summary>Multiple crefs on the same line each rewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesMultipleCrefsPerLine()
    {
        var output = Rewrite("[A][T:N.A] and [B][T:N.B]\n");
        await Assert.That(output).IsEqualTo(
            "[A](@autoref:T:N.A) and [B](@autoref:T:N.B)\n");
    }

    /// <summary>An empty label (<c>[][T:Foo]</c>) stays untouched — the rewriter requires a non-empty bracketed text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyLabelDoesNotMatch()
    {
        const string Src = "[][T:Foo]\n";
        var output = Rewrite(Src);
        await Assert.That(output).IsEqualTo(Src);
    }

    /// <summary>Source without a <c>][</c> sequence short-circuits via <see cref="AutorefsReferenceLinkPreprocessor.NeedsRewrite"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NeedsRewriteShortCircuits()
    {
        await Assert.That(AutorefsReferenceLinkPreprocessor.NeedsRewrite("just text\n"u8)).IsFalse();
        await Assert.That(AutorefsReferenceLinkPreprocessor.NeedsRewrite("[Foo][T:Bar]\n"u8)).IsTrue();

        // Format-agnostic: any reference-link shape is a candidate; the full Rewrite
        // pass still leaves entries with a matching `[label]: url` definition untouched.
        await Assert.That(AutorefsReferenceLinkPreprocessor.NeedsRewrite("[Foo][bar]\n"u8)).IsTrue();
    }

    /// <summary>Reference links pointing at a plain identifier (no commentId prefix) rewrite when no link definition resolves the label.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesAgnosticAnchorIds()
    {
        var output = Rewrite("see [the guide][installation.guide] for details\n");
        await Assert.That(output).IsEqualTo(
            "see [the guide](@autoref:installation.guide) for details\n");
    }

    /// <summary>Helper that runs the preprocessor and returns the UTF-8-decoded result.</summary>
    /// <param name="source">Input markdown.</param>
    /// <returns>Rewritten markdown.</returns>
    private static string Rewrite(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        AutorefsReferenceLinkPreprocessor.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
