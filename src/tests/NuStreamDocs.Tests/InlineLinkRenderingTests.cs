// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Inline link and image rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class InlineLinkRenderingTests
{
    /// <summary>Number of emphasis spans that push a label past the fixed-size delimiter table.</summary>
    private const int SpanRepeatCount = 20;

    /// <summary>Gets long link labels whose emphasis needs the pooled delimiter table.</summary>
    /// <returns>Source text and expected HTML of each case.</returns>
    public static IReadOnlyList<MarkdownCase> LongLabelCases()
    {
        var spans = string.Concat(Enumerable.Repeat("*x* ", SpanRepeatCount));
        var renderedSpans = string.Concat(Enumerable.Repeat("<em>x</em> ", SpanRepeatCount));
        return
        [
            new($"[a [l](m) {spans}b](u)", $"<p><a href=\"u\">a [l](m) {renderedSpans}b</a></p>\n"),
            new($"[*x* [l](m) {spans}b](u)", $"<p><a href=\"u\"><em>x</em> <a href=\"m\">l</a> {renderedSpans}b</a></p>\n"),
        ];
    }

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
    [Arguments("[l](/a\\_b\\)c)", "<a href=\"/a_b)c\">l</a>")]
    [Arguments("[l](/a\\\\b)", "<a href=\"/a\\b\">l</a>")]
    [Arguments("[l](/a\\qb)", "<a href=\"/a\\qb\">l</a>")]
    [Arguments("[l](/a\\&amp;b)", "<a href=\"/a&amp;amp;b\">l</a>")]
    [Arguments("[l](/a\\_b \"t\")", "<a href=\"/a_b\" title=\"t\">l</a>")]
    [Arguments("![i](/a\\_b.png)", "<img alt=\"i\" src=\"/a_b.png\" />")]
    [Arguments("![a](i.png)", "<img alt=\"a\" src=\"i.png\" />")]
    [Arguments("![a](i.png \"t\")", "<img alt=\"a\" src=\"i.png\" title=\"t\" />")]
    [Arguments("![](i.png)", "<img alt=\"\" src=\"i.png\" />")]
    [Arguments("![*a*](i.png)", "<img alt=\"*a*\" src=\"i.png\" />")]
    [Arguments("![a](<i 1.png>)", "<img alt=\"a\" src=\"i 1.png\" />")]
    [Arguments("![alt with *emphasis* and `code` and \"quotes\"](/img/x.png)", "<img alt=\"alt with *emphasis* and code and &quot;quotes&quot;\" src=\"/img/x.png\" />")]
    [Arguments("![a ``b ` c`` d](i.png)", "<img alt=\"a b ` c d\" src=\"i.png\" />")]
    [Arguments("![a ` b](i.png)", "<img alt=\"a ` b\" src=\"i.png\" />")]
    [Arguments("![` <b> `](i.png)", "<img alt=\"&lt;b&gt;\" src=\"i.png\" />")]
    [Arguments("![a ``b` c](i.png)", "<img alt=\"a ``b` c\" src=\"i.png\" />")]
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

    /// <summary>
    /// An inline link inside the text of another inline link renders as literal text up to the first inline element
    /// of the label; after that element, and inside emphasis, inline links stay links.
    /// </summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected paragraph content.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("[a[l](m)](u)", "<a href=\"u\">a[l](m)</a>")]
    [Arguments("[a [l](m) b](u)", "<a href=\"u\">a [l](m) b</a>")]
    [Arguments("[a [l](m) b [k](j) c](u)", "<a href=\"u\">a [l](m) b [k](j) c</a>")]
    [Arguments("[a [b [c](d) e](f) g](h)", "<a href=\"h\">a [b [c](d) e](f) g</a>")]
    [Arguments("[a [b [c](d)](f)](h)", "<a href=\"h\">a [b [c](d)](f)</a>")]
    [Arguments("[x](y) then [a [l](m)](u) end", "<a href=\"y\">x</a> then <a href=\"u\">a [l](m)</a> end")]
    [Arguments("[a [l](m)*x* b](u)", "<a href=\"u\">a [l](m)<em>x</em> b</a>")]
    [Arguments("[a [l](m) b](u) *x*", "<a href=\"u\">a [l](m) b</a> <em>x</em>")]
    [Arguments("*[a [l](m) b](u)", "*<a href=\"u\">a [l](m) b</a>")]
    [Arguments("[a [l] b](u)", "<a href=\"u\">a [l] b</a>")]
    [Arguments("[a [l](m](u)", "<a href=\"u\">a [l](m</a>")]
    [Arguments("[a [l](m) b]", "[a <a href=\"m\">l</a> b]")]
    [Arguments("[a ![i](s)[l](m)](u)", "<a href=\"u\">a <img alt=\"i\" src=\"s\" /><a href=\"m\">l</a></a>")]
    [Arguments("[a <https://x.org>[l](m) b](u)", "<a href=\"u\">a <a href=\"https://x.org\">https://x.org</a><a href=\"m\">l</a> b</a>")]
    [Arguments("[a `x` [l](m) b](u)", "<a href=\"u\">a <code>x</code> <a href=\"m\">l</a> b</a>")]
    [Arguments("[a *b* [l](m)](u)", "<a href=\"u\">a <em>b</em> <a href=\"m\">l</a></a>")]
    [Arguments("[a  \n[l](m) b](u)", "<a href=\"u\">a<br />\n<a href=\"m\">l</a> b</a>")]
    [Arguments("[a *[l](m)* b](u)", "<a href=\"u\">a <em><a href=\"m\">l</a></em> b</a>")]
    [Arguments("[a **[l](m)** b](u)", "<a href=\"u\">a <strong><a href=\"m\">l</a></strong> b</a>")]
    [Arguments("[a *b [l](m)* c](u)", "<a href=\"u\">a <em>b <a href=\"m\">l</a></em> c</a>")]
    [Arguments("*[a [l](m) b](u)*", "<em><a href=\"u\">a <a href=\"m\">l</a> b</a></em>")]
    [Arguments("_[a [l](m) b](u)_", "<em><a href=\"u\">a <a href=\"m\">l</a> b</a></em>")]
    [Arguments("*x [a [l](m) b](u)*", "<em>x <a href=\"u\">a <a href=\"m\">l</a> b</a></em>")]
    [Arguments("[![i](s)](u) [a [l](m)](v)", "<a href=\"u\"><img alt=\"i\" src=\"s\" /></a> <a href=\"v\">a [l](m)</a>")]
    [Arguments("[a [l](m) `x`](u)", "<a href=\"u\">a [l](m) <code>x</code></a>")]
    [Arguments("[a `[l](m)` b](u)", "<a href=\"u\">a <code>[l](m)</code> b</a>")]
    [Arguments("[a ![i](s) b](u)", "<a href=\"u\">a <img alt=\"i\" src=\"s\" /> b</a>")]
    [Arguments("[a <https://x.org> b](u)", "<a href=\"u\">a <a href=\"https://x.org\">https://x.org</a> b</a>")]
    public async Task InlineLinksNestOnlyAfterAnInlineElement(string markdown, string expected)
    {
        var html = Render(markdown);
        await Assert.That(html).IsEqualTo($"<p>{expected}</p>\n");
    }

    /// <summary>Reference links keep nesting with inline links, both inside a label and around one.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("[a[l][r]](u)\n\n[r]: /r\n", "<p><a href=\"u\">a<a href=\"/r\">l</a></a></p>\n")]
    [Arguments("[a [r] b](u)\n\n[r]: /r\n", "<p><a href=\"u\">a <a href=\"/r\">r</a> b</a></p>\n")]
    [Arguments("[a [r][] b](u)\n\n[r]: /r\n", "<p><a href=\"u\">a <a href=\"/r\">r</a> b</a></p>\n")]
    [Arguments("[a [l](m) b][r]\n\n[r]: /r \"t\"\n", "<p><a href=\"/r\" title=\"t\">a <a href=\"m\">l</a> b</a></p>\n")]
    [Arguments("[a [k](j) [l][r]](u)\n\n[r]: /r\n", "<p><a href=\"u\">a [k](j) <a href=\"/r\">l</a></a></p>\n")]
    [Arguments("[a [l][r] [k](j)](u)\n\n[r]: /r\n", "<p><a href=\"u\">a <a href=\"/r\">l</a> <a href=\"j\">k</a></a></p>\n")]
    [Arguments("[a [l](m) [r] b](u)\n\n[r]: /r\n", "<p><a href=\"u\">a [l](m) <a href=\"/r\">r</a> b</a></p>\n")]
    [Arguments("[l][r] then [a [k](j)](u)\n\n[r]: /r\n", "<p><a href=\"/r\">l</a> then <a href=\"u\">a [k](j)</a></p>\n")]
    public async Task ReferenceLinksNestWithInlineLinks(string markdown, string expected) =>
        await Assert.That(Render(markdown)).IsEqualTo(expected);

    /// <summary>Long link labels render inner links the same way as short ones.</summary>
    /// <param name="testCase">Source text and expected HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [MethodDataSource(nameof(LongLabelCases))]
    public async Task LongLabelsRenderInnerLinks(MarkdownCase testCase) =>
        await Assert.That(Render(testCase.Markdown)).IsEqualTo(testCase.Expected);

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
