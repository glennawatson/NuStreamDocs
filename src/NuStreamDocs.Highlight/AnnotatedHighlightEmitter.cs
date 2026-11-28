// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using NuStreamDocs.Html;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Emits syntax-highlighted HTML wrapped per line with optional line numbers, <c>.hll</c>
/// line highlights, <c>.nd-line--add|del|focus</c> diff/focus classes, and inline callout badges.
/// </summary>
internal static class AnnotatedHighlightEmitter
{
    /// <summary>Emits annotated, line-wrapped code into <paramref name="writer"/>.</summary>
    /// <param name="lexer">Optional language lexer (<c>null</c> for unknown languages).</param>
    /// <param name="cleanBody">Unescaped UTF-8 code lines without any final fence trailing newline.</param>
    /// <param name="trailingNewline">Whether the original code block ended with a trailing <c>\n</c>.</param>
    /// <param name="annotations">Optional per-line annotations (empty when none).</param>
    /// <param name="highlightedLines">Optional 1-based set of highlighted line offsets.</param>
    /// <param name="startLineNumber">Optional 1-based starting line number (<c>null</c> disables line numbers).</param>
    /// <param name="writer">Destination HTML sink.</param>
    internal static void Emit(
        Lexer? lexer,
        ReadOnlyMemory<byte> cleanBody,
        bool trailingNewline,
        LineAnnotation[] annotations,
        HashSet<int>? highlightedLines,
        int? startLineNumber,
        IBufferWriter<byte> writer)
    {
        var renderer = new LineStreamState(writer, annotations, highlightedLines, startLineNumber);
        renderer.BeginLine();

        if (lexer is null)
        {
            renderer.EmitSpan(cleanBody.Span, TokenClass.Text);
        }
        else
        {
            lexer.Tokenize(
                cleanBody.Span,
                (Source: cleanBody, Renderer: renderer),
                static (ctx, offset, length, cls) => ctx.Renderer.EmitSpan(ctx.Source.Span.Slice(offset, length), cls));
        }

        renderer.EndLine();
        if (trailingNewline)
        {
            WriteRaw(writer, "\n"u8);
        }
    }

    /// <summary>Writes raw UTF-8 bytes to <paramref name="writer"/>.</summary>
    /// <param name="writer">Destination sink.</param>
    /// <param name="bytes">UTF-8 bytes.</param>
    private static void WriteRaw(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Tracks current line index and open line wrappers across multi-line tokens.</summary>
    /// <param name="writer">Destination HTML buffer writer.</param>
    /// <param name="annotations">Per-line inline directives and callouts.</param>
    /// <param name="highlightedLines">Optional 1-based highlighted line numbers.</param>
    /// <param name="startLineNumber">Optional 1-based starting line number.</param>
    private sealed class LineStreamState(
        IBufferWriter<byte> writer,
        LineAnnotation[] annotations,
        HashSet<int>? highlightedLines,
        int? startLineNumber)
    {
        /// <summary>Scratch buffer length for formatting integer attributes.</summary>
        private const int IntScratchLength = 16;

        /// <summary>Zero-based index of the current line.</summary>
        private int _lineIndex;

        /// <summary>Gets the closing <c>&lt;/span&gt;</c> tag bytes.</summary>
        private static ReadOnlySpan<byte> SpanClose => "</span>"u8;

        /// <summary>Opens the wrapper span and optional line-number gutter for the current line.</summary>
        public void BeginLine()
        {
            var isHighlighted = highlightedLines is not null && highlightedLines.Contains(_lineIndex + 1);
            var annotation = _lineIndex < annotations.Length ? annotations[_lineIndex] : default;

            WriteRaw(writer, "<span class=\"nd-line"u8);
            if (isHighlighted)
            {
                WriteRaw(writer, " hll"u8);
            }

            WriteKindClass(annotation.Kind);
            WriteRaw(writer, "\">"u8);

            if (startLineNumber is not int startLine)
            {
                return;
            }

            WriteRaw(writer, "<span class=\"linenos\" data-line=\""u8);
            WriteInt(startLine + _lineIndex);
            WriteRaw(writer, "\"></span>"u8);
        }

        /// <summary>Emits any inline callout badge and closes the current line wrapper.</summary>
        public void EndLine()
        {
            var annotation = _lineIndex < annotations.Length ? annotations[_lineIndex] : default;
            if (annotation.CalloutNumber > 0)
            {
                WriteRaw(writer, " <span class=\"nd-code-callout\" data-callout=\""u8);
                WriteInt(annotation.CalloutNumber);
                WriteRaw(writer, "\" aria-label=\"Annotation "u8);
                WriteInt(annotation.CalloutNumber);
                WriteRaw(writer, "\">"u8);
                WriteInt(annotation.CalloutNumber);
                WriteRaw(writer, SpanClose);
            }

            WriteRaw(writer, SpanClose);
        }

        /// <summary>Emits a token span, splitting cleanly across any embedded <c>\n</c> boundaries so line spans stay strictly nested.</summary>
        /// <param name="text">Token text slice.</param>
        /// <param name="cls">Token class.</param>
        public void EmitSpan(ReadOnlySpan<byte> text, TokenClass cls)
        {
            var remaining = text;
            while (true)
            {
                var nl = remaining.IndexOf((byte)'\n');
                if (nl < 0)
                {
                    EmitSegment(remaining, cls);
                    return;
                }

                EmitSegment(remaining[..nl], cls);
                EndLine();
                WriteRaw(writer, "\n"u8);
                _lineIndex++;
                BeginLine();
                remaining = remaining[(nl + 1)..];
            }
        }

        /// <summary>Formats an integer as ASCII bytes into the writer.</summary>
        /// <param name="value">Integer value.</param>
        private void WriteInt(int value)
        {
            Span<byte> scratch = stackalloc byte[IntScratchLength];
            _ = value.TryFormat(scratch, out var written, provider: CultureInfo.InvariantCulture);
            WriteRaw(writer, scratch[..written]);
        }

        /// <summary>Writes a single-line token fragment with optional syntax class wrapper.</summary>
        /// <param name="segment">Single-line text bytes.</param>
        /// <param name="cls">Token classification.</param>
        private void EmitSegment(ReadOnlySpan<byte> segment, TokenClass cls)
        {
            if (segment.IsEmpty)
            {
                return;
            }

            var className = TokenClassNames.Css(cls);
            if (className.IsEmpty)
            {
                HtmlEscape.EscapeText(segment, writer);
                return;
            }

            WriteRaw(writer, "<span class=\""u8);
            WriteRaw(writer, className);
            WriteRaw(writer, "\">"u8);
            HtmlEscape.EscapeText(segment, writer);
            WriteRaw(writer, SpanClose);
        }

        /// <summary>Appends the modifier CSS class for <paramref name="kind"/>.</summary>
        /// <param name="kind">Line directive kind.</param>
        private void WriteKindClass(LineAnnotationKind kind)
        {
            switch (kind)
            {
                case LineAnnotationKind.Added:
                {
                    WriteRaw(writer, " nd-line--add"u8);
                    break;
                }

                case LineAnnotationKind.Deleted:
                {
                    WriteRaw(writer, " nd-line--del"u8);
                    break;
                }

                case LineAnnotationKind.Focused:
                {
                    WriteRaw(writer, " nd-line--focus"u8);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }
    }
}
