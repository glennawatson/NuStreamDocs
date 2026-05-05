// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Templating;

/// <summary>
/// Internal compiler that walks a UTF-8 template source once and emits
/// a flat <see cref="TemplateInstruction"/> array.
/// </summary>
/// <remarks>
/// All parsing happens here; <see cref="TemplateRenderer"/> only walks
/// the precomputed instructions. Section open/close pairs are matched
/// at compile time with a stack and their <c>JumpTarget</c>s are
/// patched in-place so the renderer never re-scans.
/// </remarks>
internal static class TemplateCompiler
{
    /// <summary>Initial instruction-buffer capacity floor.</summary>
    private const int MinInstructionCapacity = 8;

    /// <summary>Bytes per estimated instruction when sizing the instruction buffer.</summary>
    private const int BytesPerInstructionEstimate = 8;

    /// <summary>Initial section-stack capacity floor.</summary>
    private const int MinSectionStackCapacity = 4;

    /// <summary>Bytes per estimated section-nesting slot when sizing the stack.</summary>
    private const int BytesPerSectionEstimate = 64;

    /// <summary>Length of the <c>{{</c> open delimiter in bytes.</summary>
    private const int OpenDelimiterLength = 2;

    /// <summary>Gets the UTF-8 bytes of the open delimiter.</summary>
    private static ReadOnlySpan<byte> OpenDelim => "{{"u8;

    /// <summary>Gets the UTF-8 bytes of the close delimiter.</summary>
    private static ReadOnlySpan<byte> CloseDelim => "}}"u8;

    /// <summary>Gets the UTF-8 bytes of the triple-mustache close delimiter.</summary>
    private static ReadOnlySpan<byte> TripleCloseDelim => "}}}"u8;

    /// <summary>Compiles <paramref name="source"/> into a flat instruction list.</summary>
    /// <param name="source">UTF-8 template source.</param>
    /// <returns>The right-sized instruction array.</returns>
    public static TemplateInstruction[] Compile(ReadOnlySpan<byte> source)
    {
        var pool = ArrayPool<TemplateInstruction>.Shared;
        var buffer = pool.Rent(EstimateCapacity(source.Length));
        var openStack = ArrayPool<int>.Shared.Rent(EstimateSectionDepth(source.Length));
        var openCount = 0;
        var count = 0;
        try
        {
            var pos = 0;
            while (pos < source.Length)
            {
                var openIndex = IndexOfOpen(source, pos);
                if (openIndex < 0)
                {
                    AppendLiteral(buffer, ref count, pos, source.Length - pos);
                    break;
                }

                if (openIndex > pos)
                {
                    AppendLiteral(buffer, ref count, pos, openIndex - pos);
                }

                pos = EmitTag(source, openIndex, buffer, ref count, openStack, ref openCount);
            }

            ValidateNoOpenSections(source.Length, openStack, openCount);

            var result = new TemplateInstruction[count];
            Array.Copy(buffer, result, count);
            return result;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(openStack);
            pool.Return(buffer, clearArray: true);
        }
    }

    /// <summary>Estimates an instruction-buffer capacity from source size.</summary>
    /// <param name="length">Source byte length.</param>
    /// <returns>Conservative capacity hint.</returns>
    private static int EstimateCapacity(int length) =>
        Math.Max(MinInstructionCapacity, length / BytesPerInstructionEstimate);

    /// <summary>Estimates the maximum nesting depth from source size.</summary>
    /// <param name="length">Source byte length.</param>
    /// <returns>Conservative capacity hint.</returns>
    private static int EstimateSectionDepth(int length) =>
        Math.Max(MinSectionStackCapacity, length / BytesPerSectionEstimate);

    /// <summary>Finds the next <c>{{</c> at or after <paramref name="from"/>.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="from">Inclusive search start.</param>
    /// <returns>Absolute offset, or -1 when no further open delimiter exists.</returns>
    private static int IndexOfOpen(ReadOnlySpan<byte> source, int from)
    {
        var rel = source[from..].IndexOf(OpenDelim);
        return rel < 0 ? -1 : from + rel;
    }

    /// <summary>Appends a literal instruction.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="count">Cursor; advanced.</param>
    /// <param name="start">Literal start offset.</param>
    /// <param name="length">Literal byte length.</param>
    private static void AppendLiteral(TemplateInstruction[] buffer, ref int count, int start, int length) =>
        buffer[count++] = new(TemplateOp.Literal, start, length, JumpTarget: -1);

    /// <summary>Emits the instruction(s) for one tag and returns the cursor past it.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of the leading <c>{{</c>.</param>
    /// <param name="buffer">Instruction buffer.</param>
    /// <param name="count">Instruction count cursor.</param>
    /// <param name="openStack">Open-section index stack.</param>
    /// <param name="openCount">Open-section count.</param>
    /// <returns>Cursor positioned after the close delimiter.</returns>
    private static int EmitTag(
        ReadOnlySpan<byte> source,
        int openIndex,
        TemplateInstruction[] buffer,
        ref int count,
        int[] openStack,
        ref int openCount)
    {
        if (IsTripleOpen(source, openIndex))
        {
            return EmitTripleVariable(source, openIndex, buffer, ref count);
        }

        var nameStart = openIndex + OpenDelim.Length;
        if (nameStart >= source.Length)
        {
            throw new TemplateSyntaxException("Unterminated tag.", openIndex);
        }

        var sigil = source[nameStart];
        return sigil switch
        {
            (byte)'!' => SkipComment(source, openIndex),
            (byte)'#' => EmitSectionOpen(source, openIndex, sigil, buffer, ref count, openStack, ref openCount),
            (byte)'^' => EmitSectionOpen(source, openIndex, sigil, buffer, ref count, openStack, ref openCount),
            (byte)'/' => EmitSectionClose(source, openIndex, buffer, ref count, openStack, ref openCount),
            (byte)'&' => EmitRawVariable(source, openIndex, buffer, ref count),
            (byte)'>' => EmitPartial(source, openIndex, buffer, ref count),
            _ => EmitEscapedVariable(source, openIndex, buffer, ref count)
        };
    }

    /// <summary>True when the <c>{{</c> at <paramref name="openIndex"/> is actually a <c>{{{</c> raw open.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of the leading <c>{{</c>.</param>
    /// <returns>True when followed by a third <c>{</c>.</returns>
    private static bool IsTripleOpen(ReadOnlySpan<byte> source, int openIndex) =>
        openIndex + OpenDelimiterLength < source.Length && source[openIndex + OpenDelimiterLength] == (byte)'{';

    /// <summary>Emits a <c>{{{name}}}</c> raw-variable instruction.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of <c>{{</c>.</param>
    /// <param name="buffer">Instruction buffer.</param>
    /// <param name="count">Instruction count.</param>
    /// <returns>Cursor past the <c>}}}</c>.</returns>
    private static int EmitTripleVariable(
        ReadOnlySpan<byte> source,
        int openIndex,
        TemplateInstruction[] buffer,
        ref int count)
    {
        var nameStart = openIndex + OpenDelimiterLength + 1;
        var closeIndex = FindCloseDelimiter(source, nameStart, TripleCloseDelim, openIndex);
        var (nameStart2, nameLength) = TrimRange(source, nameStart, closeIndex);
        buffer[count++] = new(TemplateOp.RawVariable, nameStart2, nameLength, JumpTarget: -1);
        return closeIndex + TripleCloseDelim.Length;
    }

    /// <summary>Emits a <c>{{name}}</c> escaped-variable instruction.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of <c>{{</c>.</param>
    /// <param name="buffer">Instruction buffer.</param>
    /// <param name="count">Instruction count.</param>
    /// <returns>Cursor past the <c>}}</c>.</returns>
    private static int EmitEscapedVariable(
        ReadOnlySpan<byte> source,
        int openIndex,
        TemplateInstruction[] buffer,
        ref int count)
    {
        var nameStart = openIndex + OpenDelim.Length;
        var closeIndex = FindCloseDelimiter(source, nameStart, CloseDelim, openIndex);
        var (nameStart2, nameLength) = TrimRange(source, nameStart, closeIndex);
        buffer[count++] = new(TemplateOp.EscapedVariable, nameStart2, nameLength, JumpTarget: -1);
        return closeIndex + CloseDelim.Length;
    }

    /// <summary>Emits a <c>{{&amp;name}}</c> raw-variable instruction.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of <c>{{</c>.</param>
    /// <param name="buffer">Instruction buffer.</param>
    /// <param name="count">Instruction count.</param>
    /// <returns>Cursor past the <c>}}</c>.</returns>
    private static int EmitRawVariable(
        ReadOnlySpan<byte> source,
        int openIndex,
        TemplateInstruction[] buffer,
        ref int count)
    {
        var nameStart = openIndex + OpenDelim.Length + 1;
        var closeIndex = FindCloseDelimiter(source, nameStart, CloseDelim, openIndex);
        var (nameStart2, nameLength) = TrimRange(source, nameStart, closeIndex);
        buffer[count++] = new(TemplateOp.RawVariable, nameStart2, nameLength, JumpTarget: -1);
        return closeIndex + CloseDelim.Length;
    }

    /// <summary>Emits a <c>{{&gt; name }}</c> partial-inclusion instruction.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of <c>{{</c>.</param>
    /// <param name="buffer">Instruction buffer.</param>
    /// <param name="count">Instruction count.</param>
    /// <returns>Cursor past the <c>}}</c>.</returns>
    private static int EmitPartial(
        ReadOnlySpan<byte> source,
        int openIndex,
        TemplateInstruction[] buffer,
        ref int count)
    {
        var nameStart = openIndex + OpenDelim.Length + 1;
        var closeIndex = FindCloseDelimiter(source, nameStart, CloseDelim, openIndex);
        var (nameStart2, nameLength) = TrimRange(source, nameStart, closeIndex);
        buffer[count++] = new(TemplateOp.Partial, nameStart2, nameLength, JumpTarget: -1);
        return closeIndex + CloseDelim.Length;
    }

    /// <summary>Skips a <c>{{! comment }}</c> tag.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of <c>{{</c>.</param>
    /// <returns>Cursor past the <c>}}</c>.</returns>
    private static int SkipComment(ReadOnlySpan<byte> source, int openIndex)
    {
        var nameStart = openIndex + OpenDelim.Length + 1;
        var closeIndex = FindCloseDelimiter(source, nameStart, CloseDelim, openIndex);
        return closeIndex + CloseDelim.Length;
    }

    /// <summary>Emits a section-open instruction and pushes its index.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of <c>{{</c>.</param>
    /// <param name="sigil">Sigil byte (<c>#</c> or <c>^</c>).</param>
    /// <param name="buffer">Instruction buffer.</param>
    /// <param name="count">Instruction count.</param>
    /// <param name="openStack">Open-section stack.</param>
    /// <param name="openCount">Open-section count.</param>
    /// <returns>Cursor past the <c>}}</c>.</returns>
    private static int EmitSectionOpen(
        ReadOnlySpan<byte> source,
        int openIndex,
        byte sigil,
        TemplateInstruction[] buffer,
        ref int count,
        int[] openStack,
        ref int openCount)
    {
        var nameStart = openIndex + OpenDelim.Length + 1;
        var closeIndex = FindCloseDelimiter(source, nameStart, CloseDelim, openIndex);
        var (nameStart2, nameLength) = TrimRange(source, nameStart, closeIndex);
        var op = sigil == (byte)'#' ? TemplateOp.SectionOpen : TemplateOp.InvertedSectionOpen;
        openStack[openCount++] = count;
        buffer[count++] = new(op, nameStart2, nameLength, JumpTarget: -1);
        return closeIndex + CloseDelim.Length;
    }

    /// <summary>Emits a section-close instruction and patches the matching open.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="openIndex">Index of <c>{{</c>.</param>
    /// <param name="buffer">Instruction buffer.</param>
    /// <param name="count">Instruction count.</param>
    /// <param name="openStack">Open-section stack.</param>
    /// <param name="openCount">Open-section count.</param>
    /// <returns>Cursor past the <c>}}</c>.</returns>
    private static int EmitSectionClose(
        ReadOnlySpan<byte> source,
        int openIndex,
        TemplateInstruction[] buffer,
        ref int count,
        int[] openStack,
        ref int openCount)
    {
        if (openCount == 0)
        {
            throw new TemplateSyntaxException("Section close without matching open.", openIndex);
        }

        var nameStart = openIndex + OpenDelim.Length + 1;
        var closeIndex = FindCloseDelimiter(source, nameStart, CloseDelim, openIndex);
        var (nameStart2, nameLength) = TrimRange(source, nameStart, closeIndex);

        var openInstruction = openStack[--openCount];
        var openName = source.Slice(buffer[openInstruction].Start, buffer[openInstruction].Length);
        var closeName = source.Slice(nameStart2, nameLength);
        if (!openName.SequenceEqual(closeName))
        {
            throw new TemplateSyntaxException("Mismatched section close.", openIndex);
        }

        buffer[count] = new(TemplateOp.SectionClose, nameStart2, nameLength, JumpTarget: openInstruction);
        var openExisting = buffer[openInstruction];
        buffer[openInstruction] = openExisting with { JumpTarget = count };
        count++;
        return closeIndex + CloseDelim.Length;
    }

    /// <summary>Locates a close delimiter and rejects unterminated tags.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="from">Inclusive search start.</param>
    /// <param name="delimiter">Delimiter bytes.</param>
    /// <param name="openIndex">Index of the open delimiter (for error context).</param>
    /// <returns>Absolute offset of the close.</returns>
    private static int FindCloseDelimiter(
        ReadOnlySpan<byte> source,
        int from,
        ReadOnlySpan<byte> delimiter,
        int openIndex)
    {
        var rel = source[from..].IndexOf(delimiter);
        return rel < 0 ? throw new TemplateSyntaxException("Unterminated tag.", openIndex) : from + rel;
    }

    /// <summary>Trims surrounding whitespace from <paramref name="source"/> between <paramref name="start"/> and <paramref name="end"/>.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>Range descriptor over the trimmed slice.</returns>
    private static (int Start, int Length) TrimRange(ReadOnlySpan<byte> source, int start, int end)
    {
        while (start < end && source[start] is (byte)' ' or (byte)'\t')
        {
            start++;
        }

        while (end > start && source[end - 1] is (byte)' ' or (byte)'\t')
        {
            end--;
        }

        return (start, end - start);
    }

    /// <summary>Throws when any sections are still open at end-of-source.</summary>
    /// <param name="sourceLength">Source length, for the error offset.</param>
    /// <param name="openStack">Open-section stack.</param>
    /// <param name="openCount">Open-section count.</param>
    private static void ValidateNoOpenSections(int sourceLength, int[] openStack, int openCount)
    {
        if (openCount <= 0)
        {
            return;
        }

        _ = openStack;
        throw new TemplateSyntaxException("Unterminated section.", sourceLength);
    }
}
