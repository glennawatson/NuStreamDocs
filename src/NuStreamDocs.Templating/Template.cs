// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

namespace NuStreamDocs.Templating;

/// <summary>
/// A compiled, reusable Mustache-style UTF-8 template.
/// </summary>
/// <remarks>
/// Compile once, render many. The compiled instruction stream is
/// immutable and shareable across worker threads — the renderer holds
/// no per-template state and uses pooled scope buffers, so the same
/// <see cref="Template"/> instance can serve every page in a parallel
/// build.
/// </remarks>
public sealed class Template
{
    /// <summary>Original UTF-8 source kept for literal slices.</summary>
    private readonly byte[] _source;

    /// <summary>Compiled instructions.</summary>
    private readonly TemplateInstruction[] _instructions;

    /// <summary>Initializes a new instance of the <see cref="Template"/> class.</summary>
    /// <param name="source">Owning UTF-8 source.</param>
    /// <param name="instructions">Compiled instruction stream.</param>
    private Template(byte[] source, TemplateInstruction[] instructions)
    {
        _source = source;
        _instructions = instructions;
    }

    /// <summary>Gets the number of instructions in the compiled template.</summary>
    public int InstructionCount => _instructions.Length;

    /// <summary>
    /// Compiles a UTF-8 template source.
    /// </summary>
    /// <param name="source">UTF-8 template bytes.</param>
    /// <returns>The compiled template.</returns>
    /// <exception cref="TemplateSyntaxException">Thrown on malformed syntax.</exception>
    public static Template Compile(ReadOnlySpan<byte> source)
    {
        byte[] owned = [.. source];
        var instructions = TemplateCompiler.Compile(owned);
        return new(owned, instructions);
    }

    /// <summary>Renders this template against <paramref name="data"/> with no partial map.</summary>
    /// <param name="data">Root data scope.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public void Render(TemplateData data, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(writer);
        TemplateRenderer.Render(_source, _instructions, data, partials: null, writer);
    }

    /// <summary>Renders this template against <paramref name="data"/> resolving partials through <paramref name="partials"/>.</summary>
    /// <param name="data">Root data scope.</param>
    /// <param name="partials">Map of partial-name to compiled <see cref="Template"/>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public void Render(TemplateData data, FrozenDictionary<string, Template> partials, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(partials);
        ArgumentNullException.ThrowIfNull(writer);
        TemplateRenderer.Render(_source, _instructions, data, partials, writer);
    }
}
