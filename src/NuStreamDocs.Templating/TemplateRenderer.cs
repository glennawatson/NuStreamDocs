// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Html;

namespace NuStreamDocs.Templating;

/// <summary>
/// Static walker that drives a compiled instruction stream against a
/// <see cref="TemplateData"/> stack and writes UTF-8 to an
/// <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <remarks>
/// Iteration is multi-pass: when a section's key resolves to a
/// non-empty <see cref="TemplateData"/> array, the body is rendered
/// once per item, each time with that item pushed as the active scope.
/// The iteration cursor lives on a parallel frame stack so the walker
/// stays a single forward pass through the instruction stream.
/// <para>
/// Partial inclusions (<c>{{&gt; name}}</c>) recursively render the
/// matching template from the partial map under the current scope.
/// </para>
/// </remarks>
internal static class TemplateRenderer
{
    /// <summary>Initial scope-stack capacity floor.</summary>
    private const int MinScopeDepth = 8;

    /// <summary>Estimated instructions per concurrently-open scope.</summary>
    private const int InstructionsPerScopeEstimate = 4;

    /// <summary>Renders <paramref name="instructions"/> against <paramref name="root"/>.</summary>
    /// <param name="source">Original UTF-8 template source.</param>
    /// <param name="instructions">Compiled instruction stream.</param>
    /// <param name="root">Root data scope.</param>
    /// <param name="partials">Partial registry, keyed by partial name; may be null.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Render(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<TemplateInstruction> instructions,
        TemplateData root,
        Dictionary<byte[], Template>? partials,
        IBufferWriter<byte> writer)
    {
        var scopeBuffer = ArrayPool<TemplateData>.Shared.Rent(EstimateDepth(instructions.Length));
        var frameBuffer = ArrayPool<IterationFrame>.Shared.Rent(EstimateDepth(instructions.Length));
        scopeBuffer[0] = root;
        var state = new RenderState(scopeBuffer, ScopeDepth: 1, frameBuffer, FrameDepth: 0);
        try
        {
            var ip = 0;
            while (ip < instructions.Length)
            {
                ip = Step(source, instructions, ref state, partials, ip, writer);
            }
        }
        finally
        {
            ArrayPool<TemplateData>.Shared.Return(scopeBuffer, clearArray: true);
            ArrayPool<IterationFrame>.Shared.Return(frameBuffer, clearArray: true);
        }
    }

    /// <summary>Estimates the maximum scope-stack depth from instruction count.</summary>
    /// <param name="instructionCount">Number of instructions.</param>
    /// <returns>Conservative capacity hint.</returns>
    private static int EstimateDepth(int instructionCount) =>
        Math.Max(MinScopeDepth, instructionCount / InstructionsPerScopeEstimate);

    /// <summary>Executes one instruction.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="instructions">Instruction stream.</param>
    /// <param name="state">Render state (scopes + iteration frames); mutated.</param>
    /// <param name="partials">Partial registry; may be null.</param>
    /// <param name="ip">Instruction pointer.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int Step(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<TemplateInstruction> instructions,
        ref RenderState state,
        Dictionary<byte[], Template>? partials,
        int ip,
        IBufferWriter<byte> writer)
    {
        var instr = instructions[ip];
        return instr.Op switch
        {
            TemplateOp.Literal => EmitLiteral(source, instr, ip, writer),
            TemplateOp.EscapedVariable => EmitEscaped(source, instr, state.Scopes, state.ScopeDepth, ip, writer),
            TemplateOp.RawVariable => EmitRaw(source, instr, state.Scopes, state.ScopeDepth, ip, writer),
            TemplateOp.SectionOpen => EnterSection(source, instr, ref state, ip),
            TemplateOp.InvertedSectionOpen => EnterInvertedSection(source, instr, state.Scopes, state.ScopeDepth, ip),
            TemplateOp.SectionClose => ExitSection(instructions, instr, ref state, ip),
            TemplateOp.Partial => RenderPartial(source, instr, state.Scopes, state.ScopeDepth, partials, ip, writer),
            _ => ip + 1,
        };
    }

    /// <summary>Writes a literal slice to the sink.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="instr">Instruction.</param>
    /// <param name="ip">Current instruction pointer.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int EmitLiteral(ReadOnlySpan<byte> source, in TemplateInstruction instr, int ip, IBufferWriter<byte> writer)
    {
        Write(source.Slice(instr.Start, instr.Length), writer);
        return ip + 1;
    }

    /// <summary>Writes an HTML-escaped variable substitution.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="instr">Instruction.</param>
    /// <param name="scopes">Scope stack.</param>
    /// <param name="depth">Scope-stack depth.</param>
    /// <param name="ip">Current instruction pointer.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int EmitEscaped(
        ReadOnlySpan<byte> source,
        in TemplateInstruction instr,
        TemplateData[] scopes,
        int depth,
        int ip,
        IBufferWriter<byte> writer)
    {
        var key = source.Slice(instr.Start, instr.Length);
        if (TryResolveScalar(scopes, depth, key, out var value))
        {
            HtmlEscape.EscapeText(value, writer);
        }

        return ip + 1;
    }

    /// <summary>Writes an unescaped variable substitution.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="instr">Instruction.</param>
    /// <param name="scopes">Scope stack.</param>
    /// <param name="depth">Scope-stack depth.</param>
    /// <param name="ip">Current instruction pointer.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int EmitRaw(
        ReadOnlySpan<byte> source,
        in TemplateInstruction instr,
        TemplateData[] scopes,
        int depth,
        int ip,
        IBufferWriter<byte> writer)
    {
        var key = source.Slice(instr.Start, instr.Length);
        if (TryResolveScalar(scopes, depth, key, out var value))
        {
            Write(value, writer);
        }

        return ip + 1;
    }

    /// <summary>Enters a section, pushing an iteration frame when the key is an item array.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="instr">Open instruction.</param>
    /// <param name="state">Render state.</param>
    /// <param name="ip">Open-instruction pointer.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int EnterSection(
        ReadOnlySpan<byte> source,
        in TemplateInstruction instr,
        ref RenderState state,
        int ip)
    {
        var key = source.Slice(instr.Start, instr.Length);
        var current = state.Scopes[state.ScopeDepth - 1];
        var items = current.GetSection(key);

        if (items.Length > 0)
        {
            state.Frames[state.FrameDepth] = new(items, Index: 0, OpenIp: ip);
            state.Scopes[state.ScopeDepth] = items[0];
            state = state with { FrameDepth = state.FrameDepth + 1, ScopeDepth = state.ScopeDepth + 1 };
            return ip + 1;
        }

        if (!current.IsTruthy(key))
        {
            return instr.JumpTarget + 1;
        }

        state.Frames[state.FrameDepth] = new([], Index: 0, OpenIp: ip);
        state.Scopes[state.ScopeDepth] = current;
        state = state with { FrameDepth = state.FrameDepth + 1, ScopeDepth = state.ScopeDepth + 1 };
        return ip + 1;
    }

    /// <summary>Enters an inverted section when the key is falsy; otherwise jumps past it.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="instr">Open instruction.</param>
    /// <param name="scopes">Scope stack.</param>
    /// <param name="depth">Scope-stack depth.</param>
    /// <param name="ip">Open-instruction pointer.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int EnterInvertedSection(
        ReadOnlySpan<byte> source,
        in TemplateInstruction instr,
        TemplateData[] scopes,
        int depth,
        int ip)
    {
        var key = source.Slice(instr.Start, instr.Length);
        var current = scopes[depth - 1];
        return current.IsTruthy(key) ? instr.JumpTarget + 1 : ip + 1;
    }

    /// <summary>Closes a section, advancing the iteration cursor when more items remain.</summary>
    /// <param name="instructions">Instruction stream.</param>
    /// <param name="instr">Close instruction.</param>
    /// <param name="state">Render state.</param>
    /// <param name="ip">Close-instruction pointer.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int ExitSection(
        in ReadOnlySpan<TemplateInstruction> instructions,
        in TemplateInstruction instr,
        ref RenderState state,
        int ip)
    {
        var openInstr = instructions[instr.JumpTarget];
        if (openInstr.Op != TemplateOp.SectionOpen)
        {
            return ip + 1;
        }

        var frame = state.Frames[state.FrameDepth - 1];
        var nextIndex = frame.Index + 1;
        if (frame.Items.Length > 0 && nextIndex < frame.Items.Length)
        {
            state.Frames[state.FrameDepth - 1] = frame with { Index = nextIndex };
            state.Scopes[state.ScopeDepth - 1] = frame.Items[nextIndex];
            return frame.OpenIp + 1;
        }

        state = state with { FrameDepth = state.FrameDepth - 1, ScopeDepth = state.ScopeDepth - 1 };
        return ip + 1;
    }

    /// <summary>Renders a partial template under the current scope.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="instr">Partial instruction.</param>
    /// <param name="scopes">Scope stack.</param>
    /// <param name="depth">Scope-stack depth.</param>
    /// <param name="partials">Partial registry; may be null.</param>
    /// <param name="ip">Current instruction pointer.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Next instruction pointer.</returns>
    private static int RenderPartial(
        ReadOnlySpan<byte> source,
        in TemplateInstruction instr,
        TemplateData[] scopes,
        int depth,
        Dictionary<byte[], Template>? partials,
        int ip,
        IBufferWriter<byte> writer)
    {
        if (partials is null)
        {
            return ip + 1;
        }

        var key = source.Slice(instr.Start, instr.Length);
        if (!partials.TryGetValueByUtf8(key, out var template))
        {
            return ip + 1;
        }

        // Partials inherit the current scope as their root; the partial's
        // own Render method handles its instruction stream and frame
        // stack independently.
        template.Render(scopes[depth - 1], partials, writer);
        return ip + 1;
    }

    /// <summary>Walks the scope stack outermost-first, looking up a scalar by key.</summary>
    /// <param name="scopes">Scope stack.</param>
    /// <param name="depth">Stack depth.</param>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <param name="value">Resolved value on success.</param>
    /// <returns>True when any scope held the key.</returns>
    private static bool TryResolveScalar(
        TemplateData[] scopes,
        int depth,
        ReadOnlySpan<byte> key,
        out ReadOnlySpan<byte> value)
    {
        for (var i = depth - 1; i >= 0; i--)
        {
            if (scopes[i].TryGetScalar(key, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void Write(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>One frame on the iteration stack.</summary>
    /// <param name="Items">Section items being iterated; empty for a truthy-scalar section.</param>
    /// <param name="Index">Current iteration index into <paramref name="Items"/>.</param>
    /// <param name="OpenIp">Instruction pointer of the matching SectionOpen.</param>
    private readonly record struct IterationFrame(TemplateData[] Items, int Index, int OpenIp);

    /// <summary>
    /// Bundle of mutable per-render state (scope stack, iteration frames).
    /// Passed by <c>ref</c> through <see cref="Step"/> and the section
    /// helpers so the dispatcher stays narrow without growing each method's
    /// parameter list past the readability threshold.
    /// </summary>
    /// <param name="Scopes">Pooled scope-stack buffer.</param>
    /// <param name="ScopeDepth">Number of valid entries in <paramref name="Scopes"/>.</param>
    /// <param name="Frames">Pooled iteration-frame buffer.</param>
    /// <param name="FrameDepth">Number of valid entries in <paramref name="Frames"/>.</param>
    private readonly record struct RenderState(
        TemplateData[] Scopes,
        int ScopeDepth,
        IterationFrame[] Frames,
        int FrameDepth);
}
