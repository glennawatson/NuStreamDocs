// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Videos.Tests;

/// <summary>Direct branch-coverage unit tests for <see cref="VideoMarkdownScanner"/>.</summary>
public class VideoMarkdownScannerTests
{
    /// <summary>Autolink, bare URL, and trailing sentence punctuation scanners handle empty, non-HTTP, and all-punctuation inputs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareUrlAutolinkAndPunctuationScannersCoverAllBranches()
    {
        var mailtoAutolink = VideoMarkdownScanner.TryExtractBareOrAutolinkUrl("<mailto:test@example.com>"u8, out _);
        var validAutolink = VideoMarkdownScanner.TryExtractBareOrAutolinkUrl("<https://vimeo.com/76979871>"u8, out var autoUrl);
        var autoMatches = autoUrl.SequenceEqual("https://vimeo.com/76979871"u8);
        var nonUrl = VideoMarkdownScanner.TryExtractBareOrAutolinkUrl("plain-text"u8, out _);
        var bareUrl = VideoMarkdownScanner.TryExtractBareOrAutolinkUrl("http://vimeo.com/76979871?!"u8, out var trimmedBare);
        var bareMatches = trimmedBare.SequenceEqual("http://vimeo.com/76979871"u8);

        var allDotsEmpty = VideoMarkdownScanner.TrimTrailingSentencePunctuation("...!?;"u8).IsEmpty;
        var emptySpanEmpty = VideoMarkdownScanner.TrimTrailingSentencePunctuation(default).IsEmpty;

        await Assert.That(mailtoAutolink).IsFalse();
        await Assert.That(validAutolink).IsTrue();
        await Assert.That(autoMatches).IsTrue();
        await Assert.That(nonUrl).IsFalse();
        await Assert.That(bareUrl).IsTrue();
        await Assert.That(bareMatches).IsTrue();
        await Assert.That(allDotsEmpty).IsTrue();
        await Assert.That(emptySpanEmpty).IsTrue();
    }

    /// <summary>Markdown link and destination-title splitters cover empty destinations, 1-byte targets, and unmatched trailing quotes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkAndTitleSplittersCoverBoundaryInputs()
    {
        VideoMarkdownScanner.SplitDestinationAndTitle("   "u8, out var emptyHref, out var emptyTitle);
        var whitespaceEmpty = emptyHref.IsEmpty && emptyTitle.IsEmpty;

        VideoMarkdownScanner.SplitDestinationAndTitle("x"u8, out var singleHref, out var singleTitle);
        var singleOk = singleHref.SequenceEqual("x"u8) && singleTitle.IsEmpty;

        VideoMarkdownScanner.SplitDestinationAndTitle("https://example.com/path\"noopen\""u8, out var unmatchedHref, out var unmatchedTitle);
        var unmatchedOk = unmatchedHref.SequenceEqual("https://example.com/path\"noopen\""u8) && unmatchedTitle.IsEmpty;

        var unclosedBracket = VideoMarkdownScanner.TryReadMarkdownLink("[unclosed"u8, 0, out _, out _, out _, out _);
        var bracketAtEnd = VideoMarkdownScanner.TryReadMarkdownLink("[label]"u8, 0, out _, out _, out _, out _);
        var notParen = VideoMarkdownScanner.TryReadMarkdownLink("[label]x"u8, 0, out _, out _, out _, out _);
        var unclosedParen = VideoMarkdownScanner.TryReadMarkdownLink("[label](https://x"u8, 0, out _, out _, out _, out _);
        var blankDest = VideoMarkdownScanner.TryReadMarkdownLink("[label](   )"u8, 0, out _, out _, out _, out _);

        await Assert.That(whitespaceEmpty).IsTrue();
        await Assert.That(singleOk).IsTrue();
        await Assert.That(unmatchedOk).IsTrue();
        await Assert.That(unclosedBracket).IsFalse();
        await Assert.That(bracketAtEnd).IsFalse();
        await Assert.That(notParen).IsFalse();
        await Assert.That(unclosedParen).IsFalse();
        await Assert.That(blankDest).IsFalse();
    }

    /// <summary>HTML tag opener and indented-code scanners cover all tag delimiters, tab columns, and EOF termination.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlTagAndIndentedCodeScannersCoverAllDelimiters()
    {
        var tooShort = VideoMarkdownScanner.StartsWithHtmlTag("<p"u8, "pre"u8);
        var wrongName = VideoMarkdownScanner.StartsWithHtmlTag("<div>"u8, "pre"u8);
        var exactEnd = VideoMarkdownScanner.StartsWithHtmlTag("<pre"u8, "pre"u8);
        var spaceAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre class=\"x\">"u8, "pre"u8);
        var tabAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre\t>"u8, "pre"u8);
        var closeAngleAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre>"u8, "pre"u8);
        var slashAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre/>"u8, "pre"u8);
        var nulAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre\0>"u8, "pre"u8);
        var newlineAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre\n>"u8, "pre"u8);
        var bangAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre!>"u8, "pre"u8);
        var digitAfter = VideoMarkdownScanner.StartsWithHtmlTag("<pre0>"u8, "pre"u8);
        var identAfter = VideoMarkdownScanner.StartsWithHtmlTag("<prefix>"u8, "pre"u8);

        var spacesOnly = VideoMarkdownScanner.CountLeadingSpaces("   "u8, out var spacesTab);
        var tabWithinFour = VideoMarkdownScanner.CountLeadingSpaces("  \tcode"u8, out var earlyTab);
        var tabAfterFour = VideoMarkdownScanner.CountLeadingSpaces("    \tcode"u8, out var lateTab);

        const int ExpectedTwoSpaces = 2;
        const int ExpectedThreeSpaces = 3;
        const int ExpectedFourSpaces = 4;

        var codeToEof = "    line1\n\n\tline2\n    line3"u8;
        var eofEnd = VideoMarkdownScanner.ConsumeIndentedCodeRun(codeToEof, 0);
        var hitsEof = eofEnd == codeToEof.Length;

        await Assert.That(tooShort).IsFalse();
        await Assert.That(wrongName).IsFalse();
        await Assert.That(exactEnd).IsTrue();
        await Assert.That(spaceAfter).IsTrue();
        await Assert.That(tabAfter).IsTrue();
        await Assert.That(closeAngleAfter).IsTrue();
        await Assert.That(slashAfter).IsTrue();
        await Assert.That(nulAfter).IsFalse();
        await Assert.That(newlineAfter).IsFalse();
        await Assert.That(bangAfter).IsFalse();
        await Assert.That(digitAfter).IsFalse();
        await Assert.That(identAfter).IsFalse();
        await Assert.That(spacesOnly).IsEqualTo(ExpectedThreeSpaces);
        await Assert.That(spacesTab).IsFalse();
        await Assert.That(tabWithinFour).IsEqualTo(ExpectedTwoSpaces);
        await Assert.That(earlyTab).IsTrue();
        await Assert.That(tabAfterFour).IsEqualTo(ExpectedFourSpaces);
        await Assert.That(lateTab).IsFalse();
        await Assert.That(hitsEof).IsTrue();
    }
}
