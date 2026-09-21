// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace NuStreamDocs.Benchmarks;

/// <summary>Allocation profile of <c>MarkdownRenderer.Render</c> across the block and inline constructs it supports.</summary>
/// <remarks>
/// Every benchmark renders a pre-built UTF-8 document into a reused writer, so the sampled
/// allocations belong to the renderer. Tab-free and tab-indented twins of the same content
/// show the cost of the tab-expansion step.
/// </remarks>
[DebuggerDisplay("MarkdownBlockAndInlineBenchmarks: writer={_writer}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class MarkdownBlockAndInlineBenchmarks
{
    /// <summary>Approximate size of each realistic document, in bytes.</summary>
    private const int TargetDocumentBytes = 3072;

    /// <summary>Number of unmatched emphasis openers in the pathological input.</summary>
    private const int UnmatchedOpenerCount = 2500;

    /// <summary>Column width of one tab stop when converting indentation.</summary>
    private const int TabStop = 4;

    /// <summary>Initial capacity of the reused output writer.</summary>
    private const int OutputCapacity = 64 * 1024;

    /// <summary>Ordered, bullet and nested lists with fenced code, nested paragraphs and inline content; four-space indentation.</summary>
    private const string ListTemplate =
        """
        1. First step with **bold** and `code`
        2. Second step with a [link](https://example.com/a@ "Title")
            continuation line of the second step

            ```csharp
            var x = @ < 2 && y > 3;
            Console.WriteLine(x);
            ```

        3. Third step
            - nested bullet one
            - nested bullet two
                - deeper bullet
            - nested bullet three with *emphasis*

        - Bullet alpha
        - Bullet beta
            1. nested ordered a
            2. nested ordered b
        - Bullet gamma

            Paragraph inside a bullet item after a blank line.

        """;

    /// <summary>Block quotes with headings, lazy continuation, lists, nested quotes and fenced code.</summary>
    private const string BlockQuoteTemplate =
        """
        > # Quoted heading @
        > Quoted paragraph with **strong** text and a [link](https://example.com/q@ "Title").
        > continued on the next line
        lazy continuation line
        >
        > - quoted list item one
        > - quoted list item two with `code`
        >
        > > nested quote level two
        > > with more text and *emphasis*
        >
        > ```
        > code in a quote < & >
        > ```

        """;

    /// <summary>Setext and ATX headings with inline content and closing sequences.</summary>
    private const string HeadingTemplate =
        """
        Setext heading one @ with *emphasis*
        ====================================

        Paragraph text under the heading with a `code span` and more words to fill the line.

        Setext heading two @
        --------------------

        # ATX one @ with **strong** #

        ## ATX two @ ##

        ### ATX three with a [link](https://example.com/h@) ###########

        #### ATX four

        Trailing paragraph.

        """;

    /// <summary>Emphasis: single, double, triple, nested, underscore and intra-word forms.</summary>
    private const string EmphasisTemplate =
        """
        Plain *em @* and **strong @** and ***both @*** in one line, then _under @_ and __double @__.
        A **strong span with *nested em* inside** and *an em span with **nested strong** inside* too.
        Snake_case_identifier stays literal while *emphasis* next to `code *not em*` is matched.
        Escaped \*star\* and lone * star and 2 * 3 * 4 stay literal, but **all of this bold** works.

        """;

    /// <summary>Inline links with titles and angle destinations, images, URL autolinks and email autolinks.</summary>
    private const string LinkTemplate =
        """
        See the [documentation @](https://example.com/docs/@ "Documentation title") or the
        [guide](<https://example.com/a b/@> 'Guide title') and the [plain link](/relative/@).
        An image ![Alt text @](https://example.com/img/@.png "Image title") and ![no title](img/@.png).
        Visit <https://example.org/auto/@> or mail <user@@example.com> for details.
        Nested [link with **strong** and `code`](https://example.com/n@) inside text.

        """;

    /// <summary>Multi-line paragraphs with two-space and backslash hard breaks.</summary>
    private const string ParagraphTemplate =
        "First line of paragraph @ with some words  \n"
        + "second line after a two-space hard break  \n"
        + "third line after another hard break\\\n"
        + "fourth line after a backslash break\n"
        + "fifth soft-wrapped line that continues the paragraph\n"
        + "\n";

    /// <summary>Paragraphs separated by lines holding only spaces.</summary>
    private const string WhitespaceLineTemplate =
        "Paragraph @ one with **bold** text.\n"
        + "   \n"
        + "Paragraph two with a [link](https://example.com/w@).\n"
        + "      \n"
        + "- item one\n"
        + "  \n"
        + "- item two\n"
        + "    \n"
        + "> quote\n"
        + "  \n";

    /// <summary>Lists, a quote and indented code, all nested through four-space indentation.</summary>
    private const string IndentedContentTemplate =
        """
        - Outer item @
            - Inner item with `code`

                > Quote inside the inner item
                > with **strong** text

            - Second inner item

                    indented code line one < &
                    indented code line two

        1. Ordered item
            continuation paragraph text

            ```text
            fenced <code> in ordered item
            ```

        """;

    /// <summary>Reused output writer, reset per invocation so sampled bytes belong to the renderer.</summary>
    private ArrayBufferWriter<byte> _writer = null!;

    /// <summary>Lists document with spaces.</summary>
    private byte[] _lists = [];

    /// <summary>Lists document with tab indentation.</summary>
    private byte[] _listsTabs = [];

    /// <summary>Block-quote document.</summary>
    private byte[] _blockQuotes = [];

    /// <summary>Heading document.</summary>
    private byte[] _headings = [];

    /// <summary>Emphasis document.</summary>
    private byte[] _emphasis = [];

    /// <summary>Pathological run of unmatched emphasis openers.</summary>
    private byte[] _unmatchedEmphasis = [];

    /// <summary>Links, images and autolinks document.</summary>
    private byte[] _links = [];

    /// <summary>Multi-line paragraph document.</summary>
    private byte[] _paragraphs = [];

    /// <summary>Whitespace-only line document.</summary>
    private byte[] _whitespaceLines = [];

    /// <summary>Nested indented content with spaces.</summary>
    private byte[] _indentedContent = [];

    /// <summary>Nested indented content with tab indentation.</summary>
    private byte[] _indentedContentTabs = [];

    /// <summary>Builds every input document once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _writer = new(OutputCapacity);

        var lists = Repeat(ListTemplate);
        _lists = Encoding.UTF8.GetBytes(lists);
        _listsTabs = Encoding.UTF8.GetBytes(ToTabIndented(lists));
        _blockQuotes = Encoding.UTF8.GetBytes(Repeat(BlockQuoteTemplate));
        _headings = Encoding.UTF8.GetBytes(Repeat(HeadingTemplate));
        _emphasis = Encoding.UTF8.GetBytes(Repeat(EmphasisTemplate));
        _links = Encoding.UTF8.GetBytes(Repeat(LinkTemplate));
        _paragraphs = Encoding.UTF8.GetBytes(Repeat(ParagraphTemplate));
        _whitespaceLines = Encoding.UTF8.GetBytes(Repeat(WhitespaceLineTemplate));

        var indented = Repeat(IndentedContentTemplate);
        _indentedContent = Encoding.UTF8.GetBytes(indented);
        _indentedContentTabs = Encoding.UTF8.GetBytes(ToTabIndented(indented));

        const string Opener = "*a ";
        StringBuilder unmatched = new(UnmatchedOpenerCount * Opener.Length);
        for (var i = 0; i < UnmatchedOpenerCount; i++)
        {
            _ = unmatched.Append(Opener);
        }

        _unmatchedEmphasis = Encoding.UTF8.GetBytes(unmatched.ToString());
    }

    /// <summary>Renders nested lists with fenced code, spaces only.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Lists() => RenderInto(_lists);

    /// <summary>Renders the same nested lists with tab indentation.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ListsWithTabs() => RenderInto(_listsTabs);

    /// <summary>Renders block quotes containing headings, lists, nested quotes and fenced code.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BlockQuotes() => RenderInto(_blockQuotes);

    /// <summary>Renders setext and ATX headings.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Headings() => RenderInto(_headings);

    /// <summary>Renders single, nested and intra-word emphasis.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Emphasis() => RenderInto(_emphasis);

    /// <summary>Renders thousands of unmatched emphasis openers.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int UnmatchedEmphasisOpeners() => RenderInto(_unmatchedEmphasis);

    /// <summary>Renders inline links with titles, images and autolinks.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LinksImagesAndAutolinks() => RenderInto(_links);

    /// <summary>Renders multi-line paragraphs with hard breaks.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ParagraphsWithHardBreaks() => RenderInto(_paragraphs);

    /// <summary>Renders paragraphs separated by whitespace-only lines.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WhitespaceOnlyLines() => RenderInto(_whitespaceLines);

    /// <summary>Renders nested lists, quotes and indented code with spaces.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndentedContent() => RenderInto(_indentedContent);

    /// <summary>Renders the same nested content with tab indentation.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndentedContentWithTabs() => RenderInto(_indentedContentTabs);

    /// <summary>Repeats <paramref name="template"/> until the document reaches the target size, replacing <c>@</c> with the repetition index.</summary>
    /// <param name="template">Document fragment; <c>@</c> marks the index slot and <c>@@</c> a literal at-sign.</param>
    /// <returns>The assembled document.</returns>
    private static string Repeat(string template)
    {
        const string LiteralAtMarker = "\0";
        var literalAtProtected = template.Replace("@@", LiteralAtMarker, StringComparison.Ordinal);
        StringBuilder sb = new(TargetDocumentBytes + template.Length);
        for (var i = 0; sb.Length < TargetDocumentBytes; i++)
        {
            _ = sb.Append(literalAtProtected
                .Replace("@", i.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace(LiteralAtMarker, "@", StringComparison.Ordinal));
        }

        return sb.ToString();
    }

    /// <summary>Converts each line's leading spaces into tabs, one tab per four spaces.</summary>
    /// <param name="document">Space-indented document.</param>
    /// <returns>The tab-indented document.</returns>
    private static string ToTabIndented(string document)
    {
        StringBuilder sb = new(document.Length);
        foreach (var line in document.Split('\n'))
        {
            var spaces = 0;
            while (spaces < line.Length && line[spaces] is ' ')
            {
                spaces++;
            }

            _ = sb.Append('\t', spaces / TabStop)
                .Append(' ', spaces % TabStop)
                .Append(line, spaces, line.Length - spaces)
                .Append('\n');
        }

        return sb.ToString(0, sb.Length - 1);
    }

    /// <summary>Renders <paramref name="source"/> into the reused writer.</summary>
    /// <param name="source">UTF-8 markdown.</param>
    /// <returns>Bytes written.</returns>
    private int RenderInto(byte[] source)
    {
        _writer.ResetWrittenCount();
        MarkdownRenderer.Render(source, _writer);
        return _writer.WrittenCount;
    }
}
