// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Html;

namespace NuStreamDocs.Highlight;

/// <summary>Drives a <see cref="Lexer"/> over a UTF-8 byte source and writes the classed HTML directly into a UTF-8 sink.</summary>
public static class HighlightEmitter
{
    /// <summary>Tokenizes <paramref name="source"/> through <paramref name="lexer"/> and writes the classed token stream into <paramref name="writer"/>.</summary>
    /// <param name="lexer">Compiled lexer.</param>
    /// <param name="source">UTF-8 source bytes — accepts a <c>byte[]</c>, an array slice, or any other backing.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Emit(Lexer lexer, in ReadOnlyMemory<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(lexer);
        ArgumentNullException.ThrowIfNull(writer);

        var state = new EmitState(source, writer);
        lexer.Tokenize(source.Span, state, EmitFromState);
    }

    /// <summary>Static <see cref="Lexer.TokenSink{TState}"/> emitting one token from <paramref name="state"/>; method-group avoids the per-call closure alloc.</summary>
    /// <param name="state">Captured source + writer.</param>
    /// <param name="offset">Token offset.</param>
    /// <param name="length">Token length.</param>
    /// <param name="cls">Token classification.</param>
    private static void EmitFromState(EmitState state, int offset, int length, TokenClass cls) =>
        EmitToken(state.Source.Span.Slice(offset, length), cls, state.Writer);

    /// <summary>Writes a single token, with its CSS class span when classified, escaping HTML metacharacters.</summary>
    /// <param name="text">Token text bytes.</param>
    /// <param name="cls">Token classification.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitToken(ReadOnlySpan<byte> text, TokenClass cls, IBufferWriter<byte> writer)
    {
        var className = TokenClassNames.Css(cls);
        if (className.Length == 0)
        {
            HtmlEscape.EscapeText(text, writer);
            return;
        }

        Write(writer, "<span class=\""u8);
        Write(writer, className);
        Write(writer, "\">"u8);
        HtmlEscape.EscapeText(text, writer);
        Write(writer, "</span>"u8);
    }

    /// <summary>Bulk-writes UTF-8 bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
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

    /// <summary>
    /// Encapsulates the state required for token emission during the highlighting process,
    /// including the source data and the output writer.
    /// </summary>
    /// <param name="Source">UTF-8 source bytes.</param>
    /// <param name="Writer">UTF-8 sink.</param>
    private readonly record struct EmitState(ReadOnlyMemory<byte> Source, IBufferWriter<byte> Writer);
}
