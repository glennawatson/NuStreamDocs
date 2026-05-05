// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Tabs;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>TabsRewriter</c>.</summary>
public class TabsRewriterTests
{
    /// <summary>Two consecutive openers form a single tabbed-set with one input/label pair per tab.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GroupsConsecutiveOpenersIntoOneSet()
    {
        var output = Rewrite("=== \"First\"\n    one\n=== \"Second\"\n    two\n");
        await Assert.That(output).Contains("<div class=\"tabbed-set\">");
        await Assert.That(output).Contains("<label for=\"__tabbed_");
        await Assert.That(output).Contains(">First</label>");
        await Assert.That(output).Contains(">Second</label>");
    }

    /// <summary>The first tab in a set has the <c>checked</c> attribute on its radio input.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FirstTabIsCheckedByDefault()
    {
        var output = Rewrite("=== \"Solo\"\n    only\n");
        await Assert.That(output).Contains(" checked");
    }

    /// <summary>Subsequent tabs in a set do not get the <c>checked</c> attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SecondTabIsNotChecked()
    {
        var output = Rewrite("=== \"First\"\n    one\n=== \"Second\"\n    two\n");

        // Single ' checked' occurrence — only the first input gets the attribute.
        var checkedOccurrences = 0;
        var idx = 0;
        while ((idx = output.IndexOf(" checked", idx, StringComparison.Ordinal)) >= 0)
        {
            checkedOccurrences++;
            idx += " checked".Length;
        }

        await Assert.That(checkedOccurrences).IsEqualTo(1);
    }

    /// <summary>An opener with no leading <c>"</c> is rejected and passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OpenerWithoutQuoteRejected()
    {
        const string Input = "=== bare\n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>An unclosed title aborts opener parsing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedTitleRejected()
    {
        const string Input = "=== \"unclosed\n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>Trailing characters after the closing quote reject the opener.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingCharsAfterTitleRejected()
    {
        const string Input = "=== \"Title\" tail\n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>HTML-special bytes in the tab title are entity-escaped in the rendered label.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TitleEscapesHtmlSpecialBytes()
    {
        var output = Rewrite("=== \"a&b<c\"\n    body\n");
        await Assert.That(output).Contains("a&amp;b&lt;c");
    }

    /// <summary>Surrounding non-tabs text passes through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PassesThroughNonTabsText()
    {
        const string Input = "# Heading\n\nA paragraph.\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>Two openers separated by an indented fenced-code body (the mkdocs-material code-tabs pattern) merge into one tabbed-set.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GroupsOpenersWithIndentedFencedCodeBetween()
    {
        const string Input = """
            === "C#"

                ```csharp
                var x = 1;
                ```

            === "F#"

                ```fsharp
                let x = 1
                ```
            """;
        var output = Rewrite(Input);

        // Single tabbed-set wraps both labels.
        var setOpenIdx = output.IndexOf("<div class=\"tabbed-set\">", StringComparison.Ordinal);
        var nextSetIdx = setOpenIdx >= 0 ? output.IndexOf("<div class=\"tabbed-set\">", setOpenIdx + 1, StringComparison.Ordinal) : -1;
        await Assert.That(setOpenIdx).IsGreaterThanOrEqualTo(0);
        await Assert.That(nextSetIdx).IsEqualTo(-1);
        await Assert.That(output).Contains(">C#</label>");
        await Assert.That(output).Contains(">F#</label>");
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        TabsRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
