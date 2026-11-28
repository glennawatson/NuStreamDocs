// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Llms.Tests;

/// <summary>Direct UTF-8 extraction and boundary tests for <see cref="LlmsMarkdownExtractor"/>.</summary>
public class LlmsMarkdownExtractorTests
{
    /// <summary>Title, summary, section, and opt-out scanners cover front matter, ATX headings, stem humanization, and multi-byte truncation.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TitleSummarySectionAndTruncationCoverAllFallbacks()
    {
        const int TightBudget = 5;
        var emptyCreated = LlmsMarkdownExtractor.TryCreateEntry(default, "# Ignored"u8, false, LlmsOptions.Default, out _);
        var optedOutCreated = LlmsMarkdownExtractor.TryCreateEntry("page.md", "---\nllms: \"false\"\n---\n# Hi"u8, false, LlmsOptions.Default, out _);
        var explicitInSource = "---\nllms: true\nTitle: Cap Title\nsummary: Short sum\n---\nBody"u8;
        var explicitInCreated = LlmsMarkdownExtractor.TryCreateEntry("guide\\intro_step.md", explicitInSource, true, LlmsOptions.Default, out var capEntry);

        var headingTabTitle = Encoding.UTF8.GetString(LlmsMarkdownExtractor.ExtractTitle(
            "#\tTabbed Heading ###\n"u8,
            "#\tTabbed Heading ###\n"u8,
            "fallback.md"));
        var indexFolderTitle = Encoding.UTF8.GetString(LlmsMarkdownExtractor.ExtractTitle(default, default, "api-ref/index.md"));
        var rootIndexTitle = Encoding.UTF8.GetString(LlmsMarkdownExtractor.ExtractTitle(default, default, "index.md"));
        var upperStemTitle = Encoding.UTF8.GetString(LlmsMarkdownExtractor.ExtractTitle(default, default, "README.md"));
        var emptyStemTitle = Encoding.UTF8.GetString(LlmsMarkdownExtractor.ExtractTitle(default, default, ".md"));

        var emptyDefaultSection = Encoding.UTF8.GetString(LlmsMarkdownExtractor.ExtractSection("root.md", []));
        var multiByteTruncated = Encoding.UTF8.GetString(LlmsMarkdownExtractor.TruncateUtf8("Café☕world"u8, TightBudget));
        var leadingMultiByteTruncated = Encoding.UTF8.GetString(LlmsMarkdownExtractor.TruncateUtf8("🚀rocket"u8, 1));

        await Assert.That(emptyCreated).IsFalse();
        await Assert.That(optedOutCreated).IsFalse();
        await Assert.That(explicitInCreated).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(capEntry.Title)).IsEqualTo("Cap Title");
        await Assert.That(Encoding.UTF8.GetString(capEntry.Summary)).IsEqualTo("Short sum");
        await Assert.That(Encoding.UTF8.GetString(capEntry.Section)).IsEqualTo("Guide");
        await Assert.That(capEntry.RelativePath.Value).IsEqualTo("guide/intro_step.md");
        await Assert.That(headingTabTitle).IsEqualTo("Tabbed Heading");
        await Assert.That(indexFolderTitle).IsEqualTo("Api ref");
        await Assert.That(rootIndexTitle).IsEqualTo("Index");
        await Assert.That(upperStemTitle).IsEqualTo("README");
        await Assert.That(emptyStemTitle).IsEmpty();
        await Assert.That(emptyDefaultSection).IsEqualTo("Overview");
        await Assert.That(multiByteTruncated).IsEqualTo("Café...");
        await Assert.That(leadingMultiByteTruncated).IsEqualTo("...");
    }

    /// <summary>Heading and prose scanners skip backtick/tilde fences and exercise every non-prose starter byte interval.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeadingAndProseScannersSkipFencesAndNonProseMarkers()
    {
        var bytes = """
                    ```sh
                    # Not a heading
                    Not prose inside backticks
                    ```

                    ~~~yaml
                    # Also not a heading
                    Not prose inside tildes
                    ~~~

                    ###hashtag
                    #
                    > Quote
                    - Bullet
                    * Star
                    + Plus
                    | Table |
                    <div>html</div>
                    ![img](a.png)

                    # Real Heading

                    Actual first prose paragraph.
                    """u8;
        var heading = Encoding.UTF8.GetString(LlmsMarkdownExtractor.FindFirstHeading(bytes));
        var prose = Encoding.UTF8.GetString(LlmsMarkdownExtractor.FindFirstProseLine(bytes));
        var noHeading = LlmsMarkdownExtractor.FindFirstHeading("Plain text only"u8).IsEmpty;
        var unclosedFenceHeading = LlmsMarkdownExtractor.FindFirstHeading("```\n# Hidden"u8).IsEmpty;
        var noProse = LlmsMarkdownExtractor.FindFirstProseLine("# Only Heading\n- item"u8).IsEmpty;

        var controlProse = !LlmsMarkdownExtractor.FindFirstProseLine("\x01ctrl"u8).IsEmpty;
        var quoteProse = !LlmsMarkdownExtractor.FindFirstProseLine("\"quoted\" line"u8).IsEmpty;
        var dollarProse = !LlmsMarkdownExtractor.FindFirstProseLine("$100 value"u8).IsEmpty;
        var parenProse = !LlmsMarkdownExtractor.FindFirstProseLine("(note) text"u8).IsEmpty;
        var commaProse = !LlmsMarkdownExtractor.FindFirstProseLine(",leading comma"u8).IsEmpty;
        var slashProse = !LlmsMarkdownExtractor.FindFirstProseLine("/path reference"u8).IsEmpty;
        var equalsProse = !LlmsMarkdownExtractor.FindFirstProseLine("=value"u8).IsEmpty;
        var mentionProse = !LlmsMarkdownExtractor.FindFirstProseLine("@mention"u8).IsEmpty;
        var tildeShortProse = !LlmsMarkdownExtractor.FindFirstProseLine("~approx"u8).IsEmpty;

        await Assert.That(heading).IsEqualTo("Real Heading");
        await Assert.That(prose).IsEqualTo("Actual first prose paragraph.");
        await Assert.That(noHeading && unclosedFenceHeading && noProse).IsTrue();
        await Assert.That(controlProse && quoteProse && dollarProse && parenProse).IsTrue();
        await Assert.That(commaProse && slashProse && equalsProse && mentionProse && tildeShortProse).IsTrue();
    }
}
