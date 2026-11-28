// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Social.Tests;

/// <summary>Unit tests for <see cref="SocialMetadataExtractor"/>.</summary>
public sealed class SocialMetadataExtractorTests
{
    /// <summary>Expected default slug for index and empty paths.</summary>
    private const string IndexSlug = "index";

    /// <summary>Prefix length leaving one byte before the 160-byte truncation limit.</summary>
    private const int PrefixBeforeLimit = 159;

    /// <summary>Byte count exceeding the 160-byte description limit.</summary>
    private const int ContinuationBufferLength = 170;

    /// <summary>UTF-8 continuation byte value.</summary>
    private const byte ContinuationByte = 0x80;

    /// <summary>Verifies quoted and unquoted YAML frontmatter values and <c>social_image</c> priority over <c>image</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Extract_FrontMatterWithQuotesCommentsAndSocialImage_ResolvesExpectedValues()
    {
        var source =
            "---\r\n# comment\r\n  indented: ignored\r\n\r\nno_colon\r\n:leading_colon\r\nempty_val:   \r\n"u8.ToArray();
        var secondHalf =
            "title: \"Getting Started\"\r\ndescription: 'Install in seconds'\r\nimage: /first.png\r\n"u8.ToArray();
        var thirdHalf =
            "social_image: /override.png\r\nimage: /ignored-second.png\r\nother_key: ignored\r\n---\r\n# Body\r\nProse."u8.ToArray();
        byte[] combined = [.. source, .. secondHalf, .. thirdHalf];

        var meta = SocialMetadataExtractor.Extract("guide/getting-started.md", combined, "My Docs"u8);

        await Assert.That(Encoding.UTF8.GetString(meta.Title)).IsEqualTo("Getting Started");
        await Assert.That(Encoding.UTF8.GetString(meta.Description)).IsEqualTo("Install in seconds");
        await Assert.That(Encoding.UTF8.GetString(meta.ImageOverride)).IsEqualTo("/override.png");
        await Assert.That(meta.CardSlug).IsEqualTo("guide-getting-started");
    }

    /// <summary>Verifies unquoted and mismatched-quote frontmatter scalars and <c>image</c> retention when <c>social_image</c> is absent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Extract_FrontMatterWithSingleCharAndMismatchedQuotes_PreservesRawText()
    {
        var source = "---\ntitle: \"\ndescription: \"Mismatched'\nimage: /hero.png\nimage: /second-ignored.png\n---\n"u8;

        var meta = SocialMetadataExtractor.Extract("hero.md", source, default);

        await Assert.That(Encoding.UTF8.GetString(meta.Title)).IsEqualTo("\"");
        await Assert.That(Encoding.UTF8.GetString(meta.Description)).IsEqualTo("\"Mismatched'");
        await Assert.That(Encoding.UTF8.GetString(meta.ImageOverride)).IsEqualTo("/hero.png");
    }

    /// <summary>Verifies unclosed <c>---</c> frontmatter falls back to scanning the document body directly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Extract_UnclosedFrontMatter_ScansBodyDirectly()
    {
        var source = "---\n# Actual Heading ###\nFirst prose sentence with `code` and *emphasis*."u8;

        var meta = SocialMetadataExtractor.Extract("notes.md", source, default);

        await Assert.That(Encoding.UTF8.GetString(meta.Title)).IsEqualTo("Actual Heading");
        await Assert.That(Encoding.UTF8.GetString(meta.Description)).IsEqualTo("First prose sentence with code and emphasis.");
    }

    /// <summary>Verifies fenced code blocks and non-prose block starters are skipped when extracting headings and descriptions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Extract_SkipsCodeFencesListsTablesQuotesHtmlAndImages()
    {
        var source = """
            ```csharp
            # Fake heading inside backtick fence
            Fake prose inside backtick fence
            ```

            ~~~yaml
            # Fake heading inside tilde fence
            ~~~

            - list item
            * star item
            + plus item
            > quote line
            | col |
            <div>html</div>
            ![alt](pic.png)

            ## First Real Heading
            ## Second Heading Ignored

            ``inline double backtick`` starts a real prose line.
            Second prose line ignored.
            """u8;

        var meta = SocialMetadataExtractor.Extract("Reference/API_Guide.md", source, default);

        await Assert.That(Encoding.UTF8.GetString(meta.Title)).IsEqualTo("First Real Heading");
        await Assert.That(Encoding.UTF8.GetString(meta.Description)).IsEqualTo("inline double backtick starts a real prose line.");
        await Assert.That(meta.CardSlug).IsEqualTo("reference-api-guide");
    }

    /// <summary>Verifies fallback titles for <c>index.md</c>, empty paths, and stem humanization when no heading exists.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Extract_FallbackTitlesAndSlugs_CoverIndexEmptyAndHumanizedCases()
    {
        const string IndexFile = "index.md";
        var indexWithSite = SocialMetadataExtractor.Extract(IndexFile, "Plain summary."u8, "NuStreamDocs"u8);
        var indexNoSite = SocialMetadataExtractor.Extract(IndexFile, default, default);
        var emptyPath = SocialMetadataExtractor.Extract(default, default, default);
        var stemFallback = SocialMetadataExtractor.Extract("deep/quick_start---.txt", default, default);
        var trailingHyphenMd = SocialMetadataExtractor.Extract("foo---.md", "---\ndescription: Preset\n---\nx\nProse before heading\n# Heading After Prose\n"u8, default);
        var punctuationSlug = SocialMetadataExtractor.Extract("---.md", default, default);

        await Assert.That(Encoding.UTF8.GetString(indexWithSite.Title)).IsEqualTo("NuStreamDocs");
        await Assert.That(indexWithSite.CardSlug).IsEqualTo(IndexSlug);
        await Assert.That(Encoding.UTF8.GetString(indexNoSite.Title)).IsEqualTo("Home");
        await Assert.That(Encoding.UTF8.GetString(emptyPath.Title)).IsEqualTo("Home");
        await Assert.That(emptyPath.CardSlug).IsEqualTo(IndexSlug);
        await Assert.That(Encoding.UTF8.GetString(stemFallback.Title)).IsEqualTo("Quick Start   ");
        await Assert.That(stemFallback.CardSlug).IsEqualTo("deep-quick-start-txt");
        await Assert.That(trailingHyphenMd.CardSlug).IsEqualTo("foo");
        await Assert.That(Encoding.UTF8.GetString(trailingHyphenMd.Title)).IsEqualTo("Heading After Prose");
        await Assert.That(Encoding.UTF8.GetString(trailingHyphenMd.Description)).IsEqualTo("Preset");
        await Assert.That(punctuationSlug.CardSlug).IsEqualTo(IndexSlug);
    }

    /// <summary>Verifies UTF-8-safe truncation of descriptions exceeding 160 bytes, including multi-byte characters and continuation-byte edge cases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TruncateDescription_RespectsUtf8BoundariesAndContinuationRuns()
    {
        var prefix = new string('a', PrefixBeforeLimit);
        var multiByteInput = Encoding.UTF8.GetBytes($"{prefix}🚀 trailing text");
        var truncatedMultiByte = SocialMetadataExtractor.TruncateDescription(multiByteInput);

        await Assert.That(Encoding.UTF8.GetString(truncatedMultiByte)).IsEqualTo($"{prefix}...");

        var continuationOnly = new byte[ContinuationBufferLength];
        Array.Fill(continuationOnly, ContinuationByte);
        var truncatedContinuations = SocialMetadataExtractor.TruncateDescription(continuationOnly);

        await Assert.That(Encoding.UTF8.GetString(truncatedContinuations)).IsEqualTo("...");
    }
}
