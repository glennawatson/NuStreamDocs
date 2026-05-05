// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.CheckList;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>CheckListRewriter</c>.</summary>
public class CheckListRewriterTests
{
    /// <summary>An unchecked task line gains a disabled checkbox input.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesUncheckedItem()
    {
        var output = Rewrite("- [ ] todo\n");
        await Assert.That(output).Contains("- <input type=\"checkbox\" disabled>todo");
    }

    /// <summary>A checked task line gains a checked, disabled checkbox input.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesCheckedItem()
    {
        var output = Rewrite("- [x] done\n");
        await Assert.That(output).Contains("<input type=\"checkbox\" checked disabled>done");
    }

    /// <summary>Bullets that aren't followed by a task marker pass through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LeavesPlainListItemsAlone()
    {
        var output = Rewrite("- normal item\n");
        await Assert.That(output).IsEqualTo("- normal item\n");
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        CheckListRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
