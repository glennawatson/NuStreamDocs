// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Reference-style links and images resolved through the public <c>MarkdownRenderer</c> entry point.</summary>
public class ReferenceDefinitionRenderingTests
{
    /// <summary>
    /// Definitions resolve to the URL and title of every link or image that uses them, with the
    /// URL and title allowed on following lines; malformed definitions stay text.
    /// </summary>
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
    [Arguments("[a]:\n    /u\n\n[a]\n", "<p><a href=\"/u\">a</a></p>\n")]
    [Arguments("[d]:\n    http://example.com/d\n\nLink: [d]\n", "<p>Link: <a href=\"http://example.com/d\">d</a></p>\n")]
    [Arguments("[a]:\n  /u\n  \"Title\"\n\n[a]\n", "<p><a href=\"/u\" title=\"Title\">a</a></p>\n")]
    [Arguments("[a]: /u\n  'Title'\n\n[a]\n", "<p><a href=\"/u\" title=\"Title\">a</a></p>\n")]
    [Arguments("[a]: /u\nplain text\n\n[a]\n", "<p>plain text</p>\n<p><a href=\"/u\">a</a></p>\n")]
    [Arguments("[a]:\n\n[a]\n", "<p>[a]:</p>\n<p>[a]</p>\n")]
    [Arguments("[a]:", "<p>[a]:</p>\n")]
    [Arguments("[a]: /u \"unclosed\n\n[a]\n", "<p>[a]: /u &quot;unclosed</p>\n<p>[a]</p>\n")]
    [Arguments("[a]: /u trailing text\n\n[a]\n", "<p>[a]: /u trailing text</p>\n<p>[a]</p>\n")]
    public async Task ReferenceDefinitionsRender(string markdown, string expected) =>
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
