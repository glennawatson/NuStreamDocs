// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Reference-style links and images resolved through the public <c>MarkdownRenderer</c> entry point.</summary>
public class ReferenceDefinitionRenderingTests
{
    /// <summary>A definition title becomes the title attribute of every link or image that uses it.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("A [full][id] link.\n\n[id]: http://example.com \"Reference Title\"\n", "<p>A <a href=\"http://example.com\" title=\"Reference Title\">full</a> link.</p>\n")]
    [Arguments("[b]: http://example.com/b 'Title B'\n\nLink: [b]\n", "<p>Link: <a href=\"http://example.com/b\" title=\"Title B\">b</a></p>\n")]
    [Arguments("[c]: http://example.com/c (Title C)\n\nLink: [c]\n", "<p>Link: <a href=\"http://example.com/c\" title=\"Title C\">c</a></p>\n")]
    [Arguments("[a]: <http://example.com/a> \"Title A\"\n\nLink: [a]\n", "<p>Link: <a href=\"http://example.com/a\" title=\"Title A\">a</a></p>\n")]
    [Arguments("[a]: /u 'say \"hi\"'\n\n[a]\n", "<p><a href=\"/u\" title=\"say &quot;hi&quot;\">a</a></p>\n")]
    [Arguments("![full][img] and ![short]\n\n[img]: /i.png \"Full\"\n[short]: /s.png\n", "<p><img alt=\"full\" src=\"/i.png\" title=\"Full\" /> and <img alt=\"short\" src=\"/s.png\" /></p>\n")]
    [Arguments("[a]: /u\n\n[a]\n", "<p><a href=\"/u\">a</a></p>\n")]
    [Arguments("[a]: /u \"\"\n\n[a]\n", "<p><a href=\"/u\">a</a></p>\n")]
    [Arguments("[a]: /u \"unclosed\n\n[a]\n", "<p><a href=\"/u\">a</a></p>\n")]
    public async Task ReferenceTitlesRender(string markdown, string expected) =>
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
