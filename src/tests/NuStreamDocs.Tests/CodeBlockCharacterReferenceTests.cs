// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Literal handling of character references inside fenced and indented code blocks.</summary>
public class CodeBlockCharacterReferenceTests
{
    /// <summary>Character references inside fenced and indented blocks are literal text, so their ampersand is escaped.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("```\n&copy; &amp;\n```\n", "<pre><code>&amp;copy; &amp;amp;\n</code></pre>\n")]
    [Arguments("~~~\n&#35; &#x263A; &lt;\n~~~\n", "<pre><code>&amp;#35; &amp;#x263A; &amp;lt;\n</code></pre>\n")]
    [Arguments("```\na & b < c\n```\n", "<pre><code>a &amp; b &lt; c\n</code></pre>\n")]
    [Arguments("    &copy; &amp;\n", "<pre><code>&amp;copy; &amp;amp;\n</code></pre>\n")]
    [Arguments("    &#35; &lt;div&gt;\n", "<pre><code>&amp;#35; &amp;lt;div&amp;gt;\n</code></pre>\n")]
    public async Task CodeBlockCharacterReferencesAreLiteral(string markdown, string expected) =>
        await Assert.That(Render(markdown)).IsEqualTo(expected);

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
