// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Inline link and image rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class InlineLinkRenderingTests
{
    /// <summary>Links render the href and an optional quoted title; images render alt, src, and title as a self-closing tag.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected paragraph content.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("[a](/u)", "<a href=\"/u\">a</a>")]
    [Arguments("[a](/u \"t\")", "<a href=\"/u\" title=\"t\">a</a>")]
    [Arguments("[a](/u 't')", "<a href=\"/u\" title=\"t\">a</a>")]
    [Arguments("[a](  /u   \"t\"  )", "<a href=\"/u\" title=\"t\">a</a>")]
    [Arguments("[a](/u \"say \\\"hi\\\"\")", "<a href=\"/u\" title=\"say \\&quot;hi\\&quot;\">a</a>")]
    [Arguments("[a](</my url> \"t\")", "<a href=\"/my url\" title=\"t\">a</a>")]
    [Arguments("[a](<b c>)", "<a href=\"b c\">a</a>")]
    [Arguments("[a](/u \"\")", "<a href=\"/u\" title=\"\">a</a>")]
    [Arguments("[a]()", "<a href=\"\">a</a>")]
    [Arguments("[a](<>)", "<a href=\"\">a</a>")]
    [Arguments("[a](b(c))", "<a href=\"b(c)\">a</a>")]
    [Arguments("[a](/u v)", "<a href=\"/u v\">a</a>")]
    [Arguments("[a](/u \"t\" x)", "<a href=\"/u &quot;t&quot; x\">a</a>")]
    [Arguments("[a](\"only\")", "<a href=\"&quot;only&quot;\">a</a>")]
    [Arguments("[a \\] b](/u)", "<a href=\"/u\">a ] b</a>")]
    [Arguments("[a \\[ b](/u)", "<a href=\"/u\">a [ b</a>")]
    [Arguments("[a\\\\](/u)", "<a href=\"/u\">a\\</a>")]
    [Arguments("![a](i.png)", "<img alt=\"a\" src=\"i.png\" />")]
    [Arguments("![a](i.png \"t\")", "<img alt=\"a\" src=\"i.png\" title=\"t\" />")]
    [Arguments("![](i.png)", "<img alt=\"\" src=\"i.png\" />")]
    [Arguments("![*a*](i.png)", "<img alt=\"*a*\" src=\"i.png\" />")]
    [Arguments("![a](<i 1.png>)", "<img alt=\"a\" src=\"i 1.png\" />")]
    [Arguments("<https://x.com/a?b=c&d>", "<a href=\"https://x.com/a?b=c&amp;d\">https://x.com/a?b=c&amp;d</a>")]
    [Arguments("<me@x.com>", "<a href=\"mailto:me@x.com\">me@x.com</a>")]
    [Arguments("<a.b+c@sub.example.org>", "<a href=\"mailto:a.b+c@sub.example.org\">a.b+c@sub.example.org</a>")]
    [Arguments("<mailto:me@x.com>", "<a href=\"mailto:me@x.com\">mailto:me@x.com</a>")]
    public async Task LinksImagesAndAutolinksRender(string markdown, string expected) =>
        await Assert.That(Render(markdown)).IsEqualTo($"<p>{expected}</p>\n");

    /// <summary>Angle-bracket text that is not an address stays literal or raw inline HTML.</summary>
    /// <param name="markdown">Source text.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<@x.com>")]
    [Arguments("<me@>")]
    [Arguments("<me@x..com>")]
    [Arguments("<me@-x.com>")]
    [Arguments("<me@x.com->")]
    [Arguments("<me@x_y.com>")]
    [Arguments("<m e@x.com>")]
    public async Task NonAddressesAreNotMailtoLinks(string markdown) =>
        await Assert.That(Render(markdown)).DoesNotContain("mailto:");

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
