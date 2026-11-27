// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Videos.Tests;

/// <summary>Markdown syntax, code-exclusion, and option behavior tests for <see cref="VideoRewriter"/>.</summary>
public class VideoRewriterTests
{
    /// <summary>Sample short YouTube URL reused across rewriter tests.</summary>
    private const string ShortYouTubeUrl = "https://youtu.be/dQw4w9WgXcQ";

    /// <summary>Standalone bare URLs, angle-bracket autolinks, and trailing punctuation expand into responsive embeds.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StandaloneBareUrlAndAutolinkExpand()
    {
        const string Markdown = """
                                Intro paragraph.

                                https://youtu.be/dQw4w9WgXcQ.

                                <https://www.loom.com/share/abcdef123456>

                                Outro paragraph.
                                """;

        var result = RewriteString(Markdown, VideoOptions.Default);
        await Assert.That(result).Contains("Intro paragraph.\n\n<div class=\"nd-video nd-video--youtube\">");
        await Assert.That(result).Contains("src=\"https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ\"");
        await Assert.That(result).Contains("<div class=\"nd-video nd-video--loom\">");
        await Assert.That(result).Contains("src=\"https://www.loom.com/embed/abcdef123456\"");
        await Assert.That(result).Contains("Outro paragraph.");
    }

    /// <summary>Markdown image media syntax (<c>![Title](url)</c>, <c>![type:video](url)</c>, and explicit link titles) sets accessible titles.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImageMediaSyntaxUsesAltOrExplicitTitle()
    {
        const string Markdown = """
                                ![ReactiveUI Architecture](https://vimeo.com/76979871)

                                ![type:video](https://vimeo.com/76979871 "Custom Quoted Title")

                                ![type:video](https://vimeo.com/76979871)
                                """;

        var result = RewriteString(Markdown, VideoOptions.Default);
        await Assert.That(result).Contains("title=\"ReactiveUI Architecture\"");
        await Assert.That(result).Contains("title=\"Custom Quoted Title\"");
        await Assert.That(result).Contains("title=\"Vimeo video player\"");
    }

    /// <summary>Inline prose <c>[text](url)</c> hyperlinks remain untouched by default so sentence links stay anchors.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineHyperlinksInProseStayUntouched()
    {
        const string Markdown = """
                                Watch [our keynote talk](https://www.youtube.com/watch?v=dQw4w9WgXcQ) for details.

                                - [YouTube Video](https://www.youtube.com/watch?v=dQw4w9WgXcQ)
                                - [Channel](https://www.youtube.com/channel/UCWVSpLajyZcooAngLV3ItaQ)
                                """;

        var result = RewriteString(Markdown, VideoOptions.Default);
        await Assert.That(result).IsEqualTo(Markdown);
    }

    /// <summary>Opting into <see cref="VideoOptions.EmbedStandaloneLinks"/> expands standalone <c>[Title](url)</c> lines while still preserving inline links.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StandaloneMarkdownLinkExpandsOnlyWhenOptedIn()
    {
        const string Markdown = """
                                [Keynote Recording](https://www.youtube.com/watch?v=dQw4w9WgXcQ)

                                Inline [link](https://www.youtube.com/watch?v=dQw4w9WgXcQ) stays inline.
                                """;

        var defaultResult = RewriteString(Markdown, VideoOptions.Default);
        await Assert.That(defaultResult).IsEqualTo(Markdown);

        var optedIn = VideoOptions.Default.WithEmbedStandaloneLinks(true);
        var expanded = RewriteString(Markdown, optedIn);
        await Assert.That(expanded).Contains("title=\"Keynote Recording\"");
        await Assert.That(expanded).Contains("Inline [link](https://www.youtube.com/watch?v=dQw4w9WgXcQ) stays inline.");
    }

    /// <summary>Inline <c>![alt](url)</c> inside a paragraph expands while skipping inline code spans and escaped bangs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineImageVideoExpandsWhileCodeSpanAndEscapedBangPassThrough()
    {
        const string Markdown =
            "Before `![code](https://youtu.be/dQw4w9WgXcQ)` and \\![escaped](https://youtu.be/dQw4w9WgXcQ) "
            + "then ![Inline Demo](<https://youtu.be/dQw4w9WgXcQ> 'Single Quoted') after.";

        var result = RewriteString(Markdown, VideoOptions.Default);
        await Assert.That(result).Contains("`![code](https://youtu.be/dQw4w9WgXcQ)`");
        await Assert.That(result).Contains("\\![escaped](https://youtu.be/dQw4w9WgXcQ)");
        await Assert.That(result).Contains("title=\"Single Quoted\"");
        await Assert.That(result).Contains(" after.");
    }

    /// <summary>Fenced code blocks, indented code blocks, HTML comments, raw HTML tags, and <c>&lt;script&gt;</c>/<c>&lt;pre&gt;</c> blocks are never rewritten.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CodeFencesIndentedCodeCommentsAndRawHtmlArePreservedVerbatim()
    {
        const string Markdown = """
                                ```markdown
                                https://www.youtube.com/watch?v=dQw4w9WgXcQ
                                ![Demo](https://vimeo.com/76979871)
                                ```

                                ~~~
                                https://youtu.be/dQw4w9WgXcQ
                                ~~~

                                    https://www.youtube.com/watch?v=dQw4w9WgXcQ

                                <!--
                                https://www.youtube.com/watch?v=dQw4w9WgXcQ
                                -->

                                <iframe src="https://www.youtube.com/embed/dQw4w9WgXcQ"></iframe>

                                <pre>
                                https://www.youtube.com/watch?v=dQw4w9WgXcQ
                                </pre>

                                <script>
                                const url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                                </script>
                                """;

        var result = RewriteString(Markdown, VideoOptions.Default);
        await Assert.That(result).IsEqualTo(Markdown);
    }

    /// <summary>CRLF line endings and non-privacy options (<c>youtube.com</c>, no <c>dnt</c>, no <c>loading="lazy"</c>, custom container class) are honored.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomOptionsControlDomainsAttributesAndClasses()
    {
        var options = VideoOptions.Default
            .WithYouTubePrivacyEnhanced(false)
            .WithVimeoDoNotTrack(false)
            .WithLazyLoad(false)
            .WithContainerClass("custom-vid"u8);

        const string Markdown = "Paragraph\r\nhttps://youtu.be/dQw4w9WgXcQ\r\nhttps://vimeo.com/76979871\r\n";
        var result = RewriteString(Markdown, options);

        await Assert.That(result).Contains("class=\"custom-vid custom-vid--youtube\"");
        await Assert.That(result).Contains("src=\"https://www.youtube.com/embed/dQw4w9WgXcQ\"");
        await Assert.That(result).Contains("src=\"https://player.vimeo.com/video/76979871\"");
        await Assert.That(result).DoesNotContain("loading=\"lazy\"");
    }

    /// <summary>Edge-case inputs (empty source, disabled options, style/textarea blocks, unclosed blocks, balanced parens, and malformed links) round-trip safely.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EdgeCaseBlocksMalformedLinksAndFallbackContainerClassBehaveSafely()
    {
        var emptyResult = RewriteString(string.Empty, VideoOptions.Default);
        await Assert.That(emptyResult).IsEqualTo(string.Empty);

        var passthrough = RewriteString(ShortYouTubeUrl, VideoOptions.Default.WithProviders(VideoProviders.None));
        await Assert.That(passthrough).IsEqualTo(ShortYouTubeUrl);

        const string Blocks = """
                              Lead line
                                  lazy continuation https://youtu.be/dQw4w9WgXcQ

                              <style>
                              /* https://youtu.be/dQw4w9WgXcQ */
                              </style>

                              <textarea>
                              https://youtu.be/dQw4w9WgXcQ
                              </textarea>

                              <mailto:user@example.com>

                              ![Unclosed(https://youtu.be/dQw4w9WgXcQ
                              ![UnclosedParen](https://youtu.be/dQw4w9WgXcQ
                              ![Empty]()

                              ![Esc\]aped [Nested]](https://cdn.example.com/clip(1).mp4)

                              <!-- unclosed comment https://youtu.be/dQw4w9WgXcQ
                              """;

        var fallbackContainer = VideoOptions.Default with { ContainerClass = [] };
        var rewritten = RewriteString(Blocks, fallbackContainer);
        await Assert.That(rewritten).Contains("class=\"nd-video nd-video--html5\"");
        await Assert.That(rewritten).Contains("title=\"Esc\\]aped [Nested]\"");
        await Assert.That(rewritten).Contains("<!-- unclosed comment https://youtu.be/dQw4w9WgXcQ");

        const string UnclosedScript = "<script>\nhttps://youtu.be/dQw4w9WgXcQ";
        await Assert.That(RewriteString(UnclosedScript, VideoOptions.Default)).IsEqualTo(UnclosedScript);
    }

    /// <summary>Option flag permutations, newline separator boundary offsets, tab-indented blocks, and inline bang edge cases cover all rewriter branches.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionPermutationsNewlineBoundariesAndInlineBangBranchesBehaveCorrectly()
    {
        var allFormsDisabled = VideoOptions.Default
            .WithEmbedBareUrls(false)
            .WithEmbedImageLinks(false)
            .WithEmbedStandaloneLinks(false);
        await Assert.That(RewriteString(ShortYouTubeUrl, allFormsDisabled))
            .IsEqualTo(ShortYouTubeUrl);

        var onlyImageLinks = VideoOptions.Default.WithEmbedBareUrls(false);
        var imageOnlyOut = RewriteString($"{ShortYouTubeUrl}\n\n![Img](https://vimeo.com/76979871)", onlyImageLinks);
        await Assert.That(imageOnlyOut).Contains($"{ShortYouTubeUrl}\n");
        await Assert.That(imageOnlyOut).Contains("title=\"Img\"");

        var onlyStandaloneLinks = VideoOptions.Default
            .WithEmbedBareUrls(false)
            .WithEmbedImageLinks(false)
            .WithEmbedStandaloneLinks(true);
        const string MixedLinks = "![SkippedImg](https://vimeo.com/76979871)\n\nProse ![SkippedInline](https://vimeo.com/76979871)\n\n[KeptLink](https://vimeo.com/76979871 \"LinkTitle\")";
        var standaloneOnlyOut = RewriteString(MixedLinks, onlyStandaloneLinks);
        await Assert.That(standaloneOnlyOut).Contains("![SkippedImg](https://vimeo.com/76979871)");
        await Assert.That(standaloneOnlyOut).Contains("Prose ![SkippedInline](https://vimeo.com/76979871)");
        await Assert.That(standaloneOnlyOut).Contains("title=\"LinkTitle\"");

        var newlineLead = RewriteString($"\n{ShortYouTubeUrl}", VideoOptions.Default);
        var crlfLead = RewriteString($"\r\n{ShortYouTubeUrl}", VideoOptions.Default);
        var crlfDouble = RewriteString($"Text\r\n\r\n{ShortYouTubeUrl}\n\thttps://vimeo.com/76979871", VideoOptions.Default);
        var inlineEdges = RewriteString(
            $"![StartInline]({ShortYouTubeUrl}) mid !x ![broken]( ![nonvid](https://github.com/rxui) end !",
            VideoOptions.Default);

        await Assert.That(newlineLead).Contains("nd-video--youtube");
        await Assert.That(crlfLead).Contains("nd-video--youtube");
        await Assert.That(crlfDouble).Contains("\thttps://vimeo.com/76979871");
        await Assert.That(inlineEdges).Contains("title=\"StartInline\"");
        await Assert.That(inlineEdges).Contains("end !");
    }

    /// <summary>Helper that runs <see cref="VideoRewriter.Rewrite"/> on UTF-8 bytes and decodes the output.</summary>
    /// <param name="markdown">Input markdown.</param>
    /// <param name="options">Video options.</param>
    /// <returns>Rewritten markdown/HTML string.</returns>
    private static string RewriteString(string markdown, in VideoOptions options)
    {
        ArrayBufferWriter<byte> writer = new();
        VideoRewriter.Rewrite(Encoding.UTF8.GetBytes(markdown), writer, options, "localhost"u8);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
