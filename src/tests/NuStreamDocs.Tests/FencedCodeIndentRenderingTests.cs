// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Indentation of fenced code content relative to the opening fence, rendered through <c>MarkdownRenderer.Render</c>.</summary>
public class FencedCodeIndentRenderingTests
{
    /// <summary>Content lines lose the indent of the opening fence, up to that many columns.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected rendered HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("   ```\n   three\n     five\n   ```\n", "<pre><code>three\n  five\n</code></pre>\n")]
    [Arguments("  ```\n code\n  ```\n", "<pre><code>code\n</code></pre>\n")]
    [Arguments(" ~~~\n  a\n b\n c\n ~~~\n", "<pre><code> a\nb\nc\n</code></pre>\n")]
    [Arguments("```\n  kept\n```\n", "<pre><code>  kept\n</code></pre>\n")]
    public async Task FenceIndentIsStrippedFromContent(string markdown, string expected)
    {
        var html = Render(Encoding.UTF8.GetBytes(markdown));
        await Assert.That(html).IsEqualTo(expected);
    }

    /// <summary>A fence inside a list item renders its content without the item indent.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FenceInsideListItemHasNoItemIndent()
    {
        var html = Render("- item with code:\n\n    ```\n    code in item\n    ```\n\n- next item\n"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>\n<p>item with code:</p>\n<pre><code>code in item\n</code></pre>\n</li>\n<li>\n<p>next item</p>\n</li>\n</ul>\n");
    }

    /// <summary>Indentation deeper than the opening fence stays in the content.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ContentIndentBeyondFenceIsKept()
    {
        var html = Render("- item\n\n    ```\n    a\n      b\n    ```\n"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>\n<p>item</p>\n<pre><code>a\n  b\n</code></pre>\n</li>\n</ul>\n");
    }

    /// <summary>Renders <paramref name="markdown"/> to an HTML string.</summary>
    /// <param name="markdown">UTF-8 markdown.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(ReadOnlySpan<byte> markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(markdown, writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
