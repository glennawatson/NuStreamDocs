// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Toc;

/// <summary>
/// Pure renderer that turns a <see cref="Heading"/> array into a
/// <c>&lt;nav class="md-nav md-nav--secondary"&gt;</c> fragment matching
/// mkdocs-material's per-page TOC shape.
/// </summary>
/// <remarks>
/// The output structure is a strict nested <c>&lt;ul&gt;</c>/<c>&lt;li&gt;</c>
/// tree. Each list item carries an anchor pointing at the heading
/// id; nested headings open a child <c>&lt;ul&gt;</c>. The renderer
/// honours <see cref="TocOptions.MinLevel"/> / <see cref="TocOptions.MaxLevel"/>
/// to decide which headings appear in the rendered fragment — out
/// of range headings are simply skipped (their permalink anchors
/// are still emitted by <see cref="HeadingRewriter"/>).
/// </remarks>
internal static class TocFragmentRenderer
{
    /// <summary>Gets the UTF-8 entity for the <c>&amp;gt;</c> replacement.</summary>
    private static ReadOnlySpan<byte> EntityGt => "&gt;"u8;

    /// <summary>Gets the UTF-8 entity for the <c>&amp;amp;</c> replacement.</summary>
    private static ReadOnlySpan<byte> EntityAmp => "&amp;"u8;

    /// <summary>Gets the UTF-8 entity for the <c>&amp;quot;</c> replacement.</summary>
    private static ReadOnlySpan<byte> EntityQuot => "&quot;"u8;

    /// <summary>Gets the opening of the wrapper nav element.</summary>
    private static ReadOnlySpan<byte> NavOpen => "<nav class=\"md-nav md-nav--secondary\" aria-label=\"Table of contents\"><ul class=\"md-nav__list\">"u8;

    /// <summary>Gets the close of the wrapper nav element.</summary>
    private static ReadOnlySpan<byte> NavClose => "</ul></nav>"u8;

    /// <summary>Gets the list-item open tag.</summary>
    private static ReadOnlySpan<byte> LiOpen => "<li class=\"md-nav__item\">"u8;

    /// <summary>Gets the list-item close tag.</summary>
    private static ReadOnlySpan<byte> LiClose => "</li>"u8;

    /// <summary>Gets the ul open tag for nested children.</summary>
    private static ReadOnlySpan<byte> UlOpen => "<ul class=\"md-nav__list\">"u8;

    /// <summary>Gets the ul close tag.</summary>
    private static ReadOnlySpan<byte> UlClose => "</ul>"u8;

    /// <summary>Gets the anchor-open prefix including class and href.</summary>
    private static ReadOnlySpan<byte> AnchorOpenStart => "<a class=\"md-nav__link\" href=\"#"u8;

    /// <summary>Gets the anchor-open mid-section between href and title text.</summary>
    private static ReadOnlySpan<byte> AnchorOpenEnd => "\">"u8;

    /// <summary>Gets the anchor close tag.</summary>
    private static ReadOnlySpan<byte> AnchorClose => "</a>"u8;

    /// <summary>Renders <paramref name="headings"/> into <paramref name="writer"/>.</summary>
    /// <param name="snapshot">Original HTML snapshot — used to read heading inner text.</param>
    /// <param name="headings">Headings (already slug-assigned).</param>
    /// <param name="options">Plugin options (filters by min/max level).</param>
    /// <param name="writer">Output sink.</param>
    public static void Render(
        ReadOnlySpan<byte> snapshot,
        Heading[] headings,
        in TocOptions options,
        IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(headings);
        ArgumentNullException.ThrowIfNull(writer);

        var filtered = Filter(headings, options.MinLevel, options.MaxLevel);
        if (filtered.Length is 0)
        {
            return;
        }

        Write(writer, NavOpen);

        // Stack tracks the levels of currently-open <li> items (parent chain).
        // Invariants:
        //   - For each entry on the stack we have written <li><a>...</a> but not yet </li>.
        //   - Between consecutive same-level siblings we close </li> then open <li>.
        //   - When level deepens we open <ul> inside the still-open parent <li>.
        //   - When level shallows we close </li></ul> for each level dropped.
        var stack = new Stack<int>();
        for (var i = 0; i < filtered.Length; i++)
        {
            EmitHeading(snapshot, filtered[i], stack, writer);
        }

        // Drain. The first (deepest) pop is just </li>; each subsequent
        // pop closes the surrounding </ul> first (we're leaving a nested
        // level back to its parent's still-open <li>).
        var first = true;
        while (stack.TryPop(out _))
        {
            if (!first)
            {
                Write(writer, UlClose);
            }

            Write(writer, LiClose);
            first = false;
        }

        Write(writer, NavClose);
    }

    /// <summary>Closes/opens nesting around <paramref name="heading"/> and emits its list item.</summary>
    /// <param name="snapshot">Original HTML.</param>
    /// <param name="heading">Heading to emit.</param>
    /// <param name="stack">Stack of currently-open levels (mutated).</param>
    /// <param name="writer">Output sink.</param>
    private static void EmitHeading(
        ReadOnlySpan<byte> snapshot,
        in Heading heading,
        Stack<int> stack,
        IBufferWriter<byte> writer)
    {
        // Close any siblings/deeper that should not contain this heading.
        while (stack.TryPeek(out var top) && top >= heading.Level)
        {
            stack.Pop();
            Write(writer, LiClose);

            // Only close a nested <ul> when we're leaving a level that
            // was actually nested under another <li>. The outer <ul>
            // is owned by NavOpen / NavClose.
            if (top > heading.Level && stack.Count is not 0)
            {
                Write(writer, UlClose);
            }
        }

        // If we're deeper than the current parent, open a child <ul> inside its <li>.
        if (stack.TryPeek(out var parentLevel) && parentLevel < heading.Level)
        {
            Write(writer, UlOpen);
        }

        EmitItem(snapshot, in heading, writer);
        stack.Push(heading.Level);
    }

    /// <summary>Emits a single <c>&lt;li&gt;&lt;a/&gt;</c> for <paramref name="heading"/>.</summary>
    /// <param name="snapshot">Original HTML.</param>
    /// <param name="heading">Heading record.</param>
    /// <param name="writer">Sink.</param>
    private static void EmitItem(ReadOnlySpan<byte> snapshot, in Heading heading, IBufferWriter<byte> writer)
    {
        Write(writer, LiOpen);
        Write(writer, AnchorOpenStart);
        WriteSlug(writer, heading.Slug);
        Write(writer, AnchorOpenEnd);
        WriteEscapedTitle(writer, snapshot[heading.TextStart..heading.TextEnd]);
        Write(writer, AnchorClose);
    }

    /// <summary>Streams the heading inner text into <paramref name="writer"/>, stripping nested tags and escaping HTML-special bytes inline.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="inner">Bytes between the heading open and close tags.</param>
    private static void WriteEscapedTitle(IBufferWriter<byte> writer, ReadOnlySpan<byte> inner)
    {
        var i = 0;
        var runStart = 0;
        while (i < inner.Length)
        {
            var b = inner[i];

            // Nested tag — flush text-so-far, then skip past the close angle.
            if (b is (byte)'<')
            {
                FlushEscaped(writer, inner[runStart..i]);
                var closeRel = inner[i..].IndexOf((byte)'>');
                if (closeRel < 0)
                {
                    runStart = inner.Length;
                    break;
                }

                i += closeRel + 1;
                runStart = i;
                continue;
            }

            i++;
        }

        if (runStart >= inner.Length)
        {
            return;
        }

        FlushEscaped(writer, inner[runStart..]);
    }

    /// <summary>Writes <paramref name="text"/> into <paramref name="writer"/>, expanding the four HTML-special bytes to their entities.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="text">A run of UTF-8 bytes containing no <c>&lt;</c>.</param>
    private static void FlushEscaped(IBufferWriter<byte> writer, ReadOnlySpan<byte> text)
    {
        var runStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var entity = EntityFor(text[i]);
            if (entity.IsEmpty)
            {
                continue;
            }

            if (i > runStart)
            {
                Write(writer, text[runStart..i]);
            }

            Write(writer, entity);
            runStart = i + 1;
        }

        if (runStart >= text.Length)
        {
            return;
        }

        Write(writer, text[runStart..]);
    }

    /// <summary>Returns the entity span for a special byte; an empty span when the byte is plain.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>Entity bytes or empty.</returns>
    private static ReadOnlySpan<byte> EntityFor(byte b) => b switch
    {
        (byte)'>' => EntityGt,
        (byte)'&' => EntityAmp,
        (byte)'"' => EntityQuot,
        _ => default,
    };

    /// <summary>Encodes a slug (ASCII per the slugifier contract) into <paramref name="writer"/>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="slug">Slug string; ASCII-only.</param>
    private static void WriteSlug(IBufferWriter<byte> writer, string slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return;
        }

        var dst = writer.GetSpan(System.Text.Encoding.UTF8.GetMaxByteCount(slug.Length));
        var written = System.Text.Encoding.UTF8.GetBytes(slug, dst);
        writer.Advance(written);
    }

    /// <summary>Filters headings by level range.</summary>
    /// <param name="headings">Source list.</param>
    /// <param name="min">Minimum level.</param>
    /// <param name="max">Maximum level.</param>
    /// <returns>Filtered subset (new array).</returns>
    private static Heading[] Filter(Heading[] headings, int min, int max)
    {
        var count = 0;
        for (var i = 0; i < headings.Length; i++)
        {
            if (headings[i].Level >= min && headings[i].Level <= max)
            {
                count++;
            }
        }

        if (count is 0)
        {
            return [];
        }

        var result = new Heading[count];
        var idx = 0;
        for (var i = 0; i < headings.Length; i++)
        {
            if (headings[i].Level >= min && headings[i].Level <= max)
            {
                result[idx++] = headings[i];
            }
        }

        return result;
    }

    /// <summary>Bulk-writes raw bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="bytes">Bytes.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
