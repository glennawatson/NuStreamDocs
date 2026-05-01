// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Branch-coverage tests for the shared markdown byte scanner.</summary>
public class MarkdownCodeScannerTests
{
    /// <summary>AtLineStart returns true at offset 0 and after a newline; false elsewhere.</summary>
    /// <param name="source">Source text.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <param name="expected">Expected result.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("hello", 0, true)]
    [Arguments("a\nb", 2, true)]
    [Arguments("a\nb", 1, false)]
    [Arguments("ab", 1, false)]
    public async Task AtLineStartReturnsExpected(string source, int offset, bool expected) =>
        await Assert.That(MarkdownCodeScanner.AtLineStart(Encoding.UTF8.GetBytes(source), offset))
            .IsEqualTo(expected);

    /// <summary>LineEnd returns the offset just past the next newline; falls back to source length.</summary>
    /// <param name="source">Source text.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="expected">Expected line-end offset.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("abc\ndef", 0, 4)]
    [Arguments("abc\ndef", 4, 7)]
    [Arguments("no-newline", 0, 10)]
    public async Task LineEndReturnsExpected(string source, int offset, int expected) =>
        await Assert.That(MarkdownCodeScanner.LineEnd(Encoding.UTF8.GetBytes(source), offset))
            .IsEqualTo(expected);

    /// <summary>TryConsumeFence handles backticks, tildes, missing-close, and non-fence input.</summary>
    /// <param name="source">Source text.</param>
    /// <param name="expectConsumed">Expected return.</param>
    /// <param name="expectedEnd">Expected end on success.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("```\nfoo\n```\nrest", true, 12)]
    [Arguments("~~~\nfoo\n~~~\nrest", true, 12)]
    [Arguments("```\nfoo\nno close", true, 16)]
    [Arguments("not a fence", false, 0)]
    [Arguments("``", false, 0)]
    public async Task TryConsumeFenceShapes(string source, bool expectConsumed, int expectedEnd)
    {
        var ok = MarkdownCodeScanner.TryConsumeFence(Encoding.UTF8.GetBytes(source), 0, out var fenceEnd);
        await Assert.That(ok).IsEqualTo(expectConsumed);
        var observedEnd = ok ? fenceEnd : expectedEnd;
        await Assert.That(observedEnd).IsEqualTo(expectedEnd);
    }

    /// <summary>ConsumeInlineCode pairs equal-length backtick runs and falls back to single run when unmatched.</summary>
    /// <param name="source">Source text.</param>
    /// <param name="offset">Offset of the leading backtick.</param>
    /// <param name="expected">Expected end offset.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("`a`b", 0, 3)]
    [Arguments("``a``b", 0, 5)]
    [Arguments("```a```b", 0, 7)]
    [Arguments("`unclosed", 0, 1)]
    [Arguments("``mismatched`run", 0, 2)]
    public async Task ConsumeInlineCodeShapes(string source, int offset, int expected) =>
        await Assert.That(MarkdownCodeScanner.ConsumeInlineCode(Encoding.UTF8.GetBytes(source), offset))
            .IsEqualTo(expected);
}
