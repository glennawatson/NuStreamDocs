// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Branch-coverage edge cases for the inline raw-HTML scanner via InlineRenderer.</summary>
public class RawHtmlBranchTests
{
    /// <summary>Self-closing void tag passes through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SelfClosingPassThrough() =>
        await Assert.That(Render("a<br/>b")).IsEqualTo("a<br/>b");

    /// <summary>Tag attributes with quoted value pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TagWithQuotedAttribute() =>
        await Assert.That(Render("<a href=\"x\">go</a>")).Contains("<a href=\"x\">go</a>");

    /// <summary>Closing tag with whitespace before > passes through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CloseTagWithSpace() =>
        await Assert.That(Render("</a >")).Contains("</a >");

    /// <summary>Lone less-than at end of input is escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LtAtEnd() =>
        await Assert.That(Render("end<")).Contains("&lt;");

    /// <summary>Less-than followed by digit (not a tag-name byte) is escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LtFollowedByDigit() =>
        await Assert.That(Render("<5x")).Contains("&lt;5");

    /// <summary>Bang without comment leader is escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BangNotComment() =>
        await Assert.That(Render("<!fragment")).Contains("&lt;");

    /// <summary>Comment with embedded -- inside still terminates at the next -->.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CommentWithDashes() =>
        await Assert.That(Render("a<!-- a-b -->b")).Contains("<!-- a-b -->");

    /// <summary>Tag with mixed-case name passes through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedCaseTagName() =>
        await Assert.That(Render("<DiV>x</DiV>")).Contains("<DiV>");

    /// <summary>Helper that runs the inline renderer.</summary>
    /// <param name="input">Source.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string input)
    {
        var sink = new ArrayBufferWriter<byte>();
        InlineRenderer.Render(Encoding.UTF8.GetBytes(input), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
