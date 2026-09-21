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

    /// <summary>Inline links inside link text, literal and nested by an inline element, inside emphasis, and through reference links.</summary>
    private const string NestedLinkTemplate =
        """
        See [the [inner @](https://example.com/i/@) link](https://example.com/o/@ "Outer title") and [a [b [c](/d@) e](/f@) g](/h@).
        An [image ![alt @](img/@.png) then [tail](/t@) link](/u@) plus *[wrapped [inner](/w@) link](/x@)* and [code `x` [tail](/y@)](/z@).
        Reference [outer [in][r@] text](/o@) beside a [label [x](/x@) here][r@] and [short [s@] link](/s@).

        [r@]: https://example.com/r/@
        [s@]: /short/@

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

    /// <summary>Inline code spans, fenced code and indented code holding ampersands and character references, next to plain text references.</summary>
    private const string CharacterReferenceTemplate =
        """
        Inline `&copy; &amp; <b>&lt;` and ``a & b &#169;`` beside plain &copy; &#169; & text @.

        ```html
        <p class="x">&copy; & &lt; @</p>
        ```

        ~~~
        tilde &amp; fence &#x41; "quoted"
        ~~~

            indented &copy; code & <tag> @
            second &amp; line

        """;

    /// <summary>Code spans with padding, doubled fences, blank spans, unmatched backtick runs and escaped backticks.</summary>
    private const string CodeSpanTemplate =
        """
        Spans ` padded @ ` and `` double `tick` inside `` and ` a  b ` with an unmatched ``` triple run and more text.
        Escaped \* star and `code with **stars** and <tags> & amp` next to ``x`` and ` ` blank span @.


        """;

    /// <summary>Reference links, collapsed and shortcut references, reference images, titled definitions and definitions split across lines.</summary>
    private const string ReferenceLinkTemplate =
        """
        See [the docs][docs@], the [guide][], and [shortcut@] plus ![logo][logo@] and [missing][none@] and [plain@].

        [docs@]: https://example.com/docs/@ "Documentation title"
        [guide]: <https://example.com/guide/@ path> 'Guide title'
        [shortcut@]: /short/@ (Paren title)
        [logo@]:
            /img/@.png
            "Logo title on the next line"
        [plain@]: /plain/@
        [broken@]: /broken trailing text


        """;

    /// <summary>Links and images whose labels hold escaped brackets and whose destinations hold backslash escapes.</summary>
    private const string EscapedLinkTemplate =
        """
        A [link with \] escaped bracket @](/u/@) and [another \[nested\] label](/v/@ "Title") here.
        Destination escapes [one](/a\_b\)c/@) and [two](/path\\with\/slash@) and [three](/keep\backslash@) end.
        An image ![alt \] text](/img\_@.png) and ![x](/i\(1\)@.png "T") close.


        """;

    /// <summary>Images whose alt text holds code spans, emphasis and quotes, with double, single and parenthesized titles.</summary>
    private const string ImageAltTemplate =
        """
        ![alt with `code` inside @](/img/@.png "Title") and ![two `a` and ``b`c`` spans](/img/b@.png 'Single').
        ![unmatched ` backtick @](/img/c.png) and ![emphasis *stays* and "quotes" & amp](/img/d@.png (Paren title)).
        ![](/img/e@.png) and ![ `  padded  ` ](/img/f.png "Title with \"escaped\" quotes").


        """;

    /// <summary>ATX headings indented by one to three spaces, with closing sequences, trailing spaces and escaped closers.</summary>
    private const string AtxHeadingTemplate =
        """
         # One space @ #

          ## Two spaces @ ##

           ### Three spaces with *em* and `code` ###

        # Closing hashes # ##########

        ## Trailing spaces after closer @ ##

        #### Hash inside # text @ #

        ###### Level six @

        # Escaped closer \#

        """;

    /// <summary>Setext headings spanning several lines, indented setext headings and a paragraph followed by an underline.</summary>
    private const string SetextHeadingTemplate =
        """
        Multi-line setext heading @
        continues on a second line with *emphasis*
        =========================================

        Another multi-line heading @ with `code`
        second line of the heading
        third line
        -----

           Indented setext @
           ===

        Paragraph then rule @
        ---

        """;

    /// <summary>Fenced code indented by one to three spaces and fenced code inside bullet and ordered list items.</summary>
    private const string IndentedFenceTemplate =
        """
         ```csharp
         var one = @;
           var indented = "two";
         ```

          ~~~text
          two-space fence @ < & >
            extra indent kept
          ~~~

           ```
           three-space fence
          under-indented line
           ```

        - list item @

          ```python
          print("fence in list item @")
              nested = 1
          ```

        1. ordered item

           ~~~
           tilde fence in ordered item < >
           ~~~

        """;

    /// <summary>Nested block quotes with lazy continuation lines after inner quotes, lists and emphasis.</summary>
    private const string NestedQuoteTemplate =
        """
        > outer quote @
        > > inner quote line
        lazy line after inner
        > > > third level
        lazy line after third level
        >
        > > back to level two
        continues lazily

        > quoted paragraph
        > - list in quote
        lazy after list item text
        > > nested with **strong**
        also lazy *emphasis*


        """;

    /// <summary>Block quote lines with tabs after the quote markers.</summary>
    private const string QuoteTabTemplate =
        ">\tquoted with tab after marker @\n"
        + ">\n"
        + ">\t\ttwo tabs after marker\n"
        + ">\n"
        + "> \tspace then tab\n"
        + ">\n"
        + ">\t> nested after tab\n"
        + "> >\tnested marker then tab\n"
        + ">\n"
        + ">\t- list after tab\n"
        + ">\n"
        + ">\t    indented code inside quote\n"
        + "\n";

    /// <summary>Triple, mixed and nested emphasis in asterisk and underscore forms, intra-word runs and unbalanced runs.</summary>
    private const string TripleEmphasisTemplate =
        """
        Triple ***a @*** and ___b @___ done.

        Mixed ***a** b* here @.

        Mixed ***a* b** here @.

        Nested **a *b* c** and *a **b** c* plus _a **b** c_ and __a *b* c__ end.

        Intra-word foo***bar***baz and foo___bar___baz stay partly literal, ****four**** and ______six______.

        Unbalanced ***open only and closer only*** with **strong *and em*** @.


        """;

    /// <summary>URL autolinks, email autolinks, and angle-bracket text that is not an autolink.</summary>
    private const string AutolinkTemplate =
        """
        Visit <https://example.com/a/@?q=1&r=2> or <http://example.org/@> or <ftp://files.example.com/@>.
        Mail <user@@example.com> or <first.last+tag@@sub.example.org> or <a-b_c@@x.io> today.
        Not links: <not an autolink @> and <a@@b> and <> and < https://spaced.example > and <notascheme:> end.
        Inline <b>html</b> stays and <span class="x">span</span> too.


        """;

    /// <summary>Quotes containing lists and fenced code, and list items containing quotes and nested quotes.</summary>
    private const string QuoteAndListTemplate =
        """
        > - quoted item one @
        > - quoted item two
        >   continued in the item
        >
        > 1. ordered in quote
        > 2. second ordered
        >    ```
        >    code in quoted item
        >    ```
        >
        > > - nested quote list
        > > - second

        - item with quote @
          > quoted in item
          > more quote
        - item with nested quote list
          > - list in quote in item
          > - second

        1. ordered
           > quote
           > > nested quote
           - nested bullet


        """;

    /// <summary>List items continued by unindented lazy lines and empty bullet items.</summary>
    private const string LazyAndEmptyItemTemplate =
        """
        - lazy item @
        continues without indent
        - next item

        1. ordered lazy
        still the same paragraph
        2. second

        -
        - empty bullet above @
        -

        * star item
        * another

        + plus item


        """;

    /// <summary>One-line list items separated by blank lines, including an item that ends in a space.</summary>
    private const string BlankSeparatedItemTemplate =
        "- one-line item @\n"
        + "\n"
        + "- second one-line item\n"
        + "\n"
        + "\n"
        + "- item ending with space \n"
        + "  \n"
        + "- last item\n"
        + "\n"
        + "1. ordered one-line @\n"
        + "\n"
        + "2. ordered two\n"
        + "\n";

    /// <summary>Single line of code repeated inside a fence that is never closed.</summary>
    private const string UnclosedFenceLine = "code line @ < & > with \"quotes\"\n";

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

    /// <summary>Links nested inside link text document.</summary>
    private byte[] _nestedLinks = [];

    /// <summary>Multi-line paragraph document.</summary>
    private byte[] _paragraphs = [];

    /// <summary>Whitespace-only line document.</summary>
    private byte[] _whitespaceLines = [];

    /// <summary>Nested indented content with spaces.</summary>
    private byte[] _indentedContent = [];

    /// <summary>Nested indented content with tab indentation.</summary>
    private byte[] _indentedContentTabs = [];

    /// <summary>Character references inside code and plain text document.</summary>
    private byte[] _characterReferences = [];

    /// <summary>Code span trimming and unmatched backtick document.</summary>
    private byte[] _codeSpans = [];

    /// <summary>Reference links, titles and definitions document.</summary>
    private byte[] _referenceLinks = [];

    /// <summary>Escaped bracket and destination escape document.</summary>
    private byte[] _escapedLinks = [];

    /// <summary>Image alt text and title document.</summary>
    private byte[] _imageAlt = [];

    /// <summary>Indented ATX heading document.</summary>
    private byte[] _atxHeadings = [];

    /// <summary>Multi-line setext heading document.</summary>
    private byte[] _setextHeadings = [];

    /// <summary>Indented and list-nested fenced code document.</summary>
    private byte[] _indentedFences = [];

    /// <summary>Fence that never closes.</summary>
    private byte[] _unclosedFence = [];

    /// <summary>Nested block quotes with lazy lines.</summary>
    private byte[] _nestedQuotes = [];

    /// <summary>Block quotes with tabs after the markers.</summary>
    private byte[] _quoteTabs = [];

    /// <summary>Triple and mixed emphasis document.</summary>
    private byte[] _tripleEmphasis = [];

    /// <summary>URL and email autolink document.</summary>
    private byte[] _autolinks = [];

    /// <summary>Quotes and lists nested in each other.</summary>
    private byte[] _quoteAndLists = [];

    /// <summary>Lazy list continuation and empty item document.</summary>
    private byte[] _lazyAndEmptyItems = [];

    /// <summary>One-line items separated by blank lines.</summary>
    private byte[] _blankSeparatedItems = [];

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
        _nestedLinks = Encoding.UTF8.GetBytes(Repeat(NestedLinkTemplate));
        _paragraphs = Encoding.UTF8.GetBytes(Repeat(ParagraphTemplate));
        _whitespaceLines = Encoding.UTF8.GetBytes(Repeat(WhitespaceLineTemplate));

        var indented = Repeat(IndentedContentTemplate);
        _indentedContent = Encoding.UTF8.GetBytes(indented);
        _indentedContentTabs = Encoding.UTF8.GetBytes(ToTabIndented(indented));

        _characterReferences = Encoding.UTF8.GetBytes(Repeat(CharacterReferenceTemplate));
        _codeSpans = Encoding.UTF8.GetBytes(Repeat(CodeSpanTemplate));
        _referenceLinks = Encoding.UTF8.GetBytes(Repeat(ReferenceLinkTemplate));
        _escapedLinks = Encoding.UTF8.GetBytes(Repeat(EscapedLinkTemplate));
        _imageAlt = Encoding.UTF8.GetBytes(Repeat(ImageAltTemplate));
        _atxHeadings = Encoding.UTF8.GetBytes(Repeat(AtxHeadingTemplate));
        _setextHeadings = Encoding.UTF8.GetBytes(Repeat(SetextHeadingTemplate));
        _indentedFences = Encoding.UTF8.GetBytes(Repeat(IndentedFenceTemplate));
        _unclosedFence = Encoding.UTF8.GetBytes($"Intro paragraph before the fence.\n\n```csharp\n{Repeat(UnclosedFenceLine)}");
        _nestedQuotes = Encoding.UTF8.GetBytes(Repeat(NestedQuoteTemplate));
        _quoteTabs = Encoding.UTF8.GetBytes(Repeat(QuoteTabTemplate));
        _tripleEmphasis = Encoding.UTF8.GetBytes(Repeat(TripleEmphasisTemplate));
        _autolinks = Encoding.UTF8.GetBytes(Repeat(AutolinkTemplate));
        _quoteAndLists = Encoding.UTF8.GetBytes(Repeat(QuoteAndListTemplate));
        _lazyAndEmptyItems = Encoding.UTF8.GetBytes(Repeat(LazyAndEmptyItemTemplate));
        _blankSeparatedItems = Encoding.UTF8.GetBytes(Repeat(BlankSeparatedItemTemplate));

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

    /// <summary>Renders inline and reference links nested inside link text.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NestedInlineLinks() => RenderInto(_nestedLinks);

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

    /// <summary>Renders character references inside code spans, fenced code and indented code.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CharacterReferencesInCode() => RenderInto(_characterReferences);

    /// <summary>Renders code spans with padding and unmatched backtick runs.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CodeSpans() => RenderInto(_codeSpans);

    /// <summary>Renders reference links, reference images and titled or multi-line definitions.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReferenceLinks() => RenderInto(_referenceLinks);

    /// <summary>Renders links with escaped brackets in the text and backslash escapes in destinations.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EscapedLinkPunctuation() => RenderInto(_escapedLinks);

    /// <summary>Renders images with code spans in the alt text and image titles.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ImageAltTextAndTitles() => RenderInto(_imageAlt);

    /// <summary>Renders ATX headings with leading spaces and closing sequences.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AtxHeadingsWithIndent() => RenderInto(_atxHeadings);

    /// <summary>Renders multi-line setext headings.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SetextHeadingsMultiLine() => RenderInto(_setextHeadings);

    /// <summary>Renders fenced code indented up to three spaces and fenced code inside list items.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndentedFencedCode() => RenderInto(_indentedFences);

    /// <summary>Renders a fenced code block that never closes.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int UnclosedFence() => RenderInto(_unclosedFence);

    /// <summary>Renders nested block quotes with lazy continuation lines.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NestedQuotesWithLazyLines() => RenderInto(_nestedQuotes);

    /// <summary>Renders block quotes with tabs after the quote markers.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TabsAfterQuoteMarkers() => RenderInto(_quoteTabs);

    /// <summary>Renders triple, mixed and nested emphasis in both marker forms.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TripleAndMixedEmphasis() => RenderInto(_tripleEmphasis);

    /// <summary>Renders URL and email autolinks.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Autolinks() => RenderInto(_autolinks);

    /// <summary>Renders quotes nested in lists and lists nested in quotes.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int QuotesAndListsCombined() => RenderInto(_quoteAndLists);

    /// <summary>Renders lazy list continuation lines and empty bullet items.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LazyAndEmptyListItems() => RenderInto(_lazyAndEmptyItems);

    /// <summary>Renders one-line list items separated by blank lines.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BlankSeparatedListItems() => RenderInto(_blankSeparatedItems);

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
