// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Toc.Tests;

/// <summary>Parameterised inputs for HeadingScanner.Scan covering heading level, id quoting, and stop-byte branches.</summary>
public class HeadingScannerParameterisedTests
{
    /// <summary>Heading levels h1..h6 are all recognised.</summary>
    /// <param name="level">Heading level digit.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task HeadingLevelRecognised(int level)
    {
        var html = Encoding.UTF8.GetBytes($"<h{level}>x</h{level}>");
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(1);
        await Assert.That(headings[0].Level).IsEqualTo(level);
    }

    /// <summary>Out-of-range level digits are rejected.</summary>
    /// <param name="level">Bogus level.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(7)]
    [Arguments(9)]
    public async Task OutOfRangeLevelsIgnored(int level)
    {
        var html = Encoding.UTF8.GetBytes($"<h{level}>x</h{level}>");
        await Assert.That(HeadingScanner.Scan(html).Length).IsEqualTo(0);
    }

    /// <summary>Quoted id attribute styles all round-trip the value.</summary>
    /// <param name="quote">Quote character.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments('"')]
    [Arguments('\'')]
    public async Task QuotedIdRoundTrips(char quote)
    {
        var html = Encoding.UTF8.GetBytes($"<h2 id={quote}target{quote}>x</h2>");
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings[0].ExistingId).IsEqualTo("target");
    }

    /// <summary>Unquoted id stops at every recognised stop byte.</summary>
    /// <param name="trailing">Trailing whitespace/close character.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(' ')]
    [Arguments('\t')]
    [Arguments('>')]
    public async Task UnquotedIdStopBytes(char trailing)
    {
        var html = Encoding.UTF8.GetBytes($"<h2 id=plain{trailing}class=\"x\">body</h2>");
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings[0].ExistingId).IsEqualTo("plain");
    }

    /// <summary>Mixed-case <c>h</c> in the close tag still terminates the heading.</summary>
    /// <param name="openTag">Opener.</param>
    /// <param name="closeTag">Closer.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<h2>", "</h2>")]
    [Arguments("<H2>", "</H2>")]
    [Arguments("<h2>", "</H2>")]
    public async Task CaseInsensitiveCloseTag(string openTag, string closeTag)
    {
        var html = Encoding.UTF8.GetBytes($"{openTag}body{closeTag}");
        await Assert.That(HeadingScanner.Scan(html).Length).IsEqualTo(1);
    }
}
