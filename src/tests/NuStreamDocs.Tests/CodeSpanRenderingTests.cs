// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Code span rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class CodeSpanRenderingTests
{
    /// <summary>Code spans escape their content and trim surrounding whitespace.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected paragraph content.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("`a`", "<code>a</code>")]
    [Arguments("` a `", "<code>a</code>")]
    [Arguments("`  a b  `", "<code>a b</code>")]
    [Arguments("`` a`b ``", "<code>a`b</code>")]
    [Arguments("`` ` ``", "<code>`</code>")]
    [Arguments("`a<b>&c`", "<code>a&lt;b&gt;&amp;c</code>")]
    [Arguments("`*not em*`", "<code>*not em*</code>")]
    [Arguments("`a\\`", "<code>a\\</code>")]
    [Arguments("`a", "`a")]
    [Arguments("``a`", "``a`")]
    public async Task CodeSpansRender(string markdown, string expected) =>
        await Assert.That(Render(markdown)).IsEqualTo($"<p>{expected}</p>\n");

    /// <summary>Renders <paramref name="markdown"/> to an HTML string.</summary>
    /// <param name="markdown">Markdown text.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(Encoding.UTF8.GetBytes(markdown), writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
