// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Html;

namespace NuStreamDocs.Highlight;

/// <summary>Drives a <see cref="Lexer"/> over a source string and writes the classed HTML directly into a UTF-8 sink.</summary>
public static class HighlightEmitter
{
    /// <summary>Tokenises <paramref name="source"/> through <paramref name="lexer"/> and writes the classed token stream into <paramref name="writer"/>.</summary>
    /// <param name="lexer">Compiled lexer.</param>
    /// <param name="source">Source code (UTF-16).</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Emit(Lexer lexer, string source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(lexer);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(writer);

        var state = new EmitState(source, writer);
        lexer.Tokenise(source, state, EmitFromState);
    }

    /// <summary>Static <see cref="Lexer.TokenSink{TState}"/> emitting one token from <paramref name="state"/>; method-group avoids the per-call closure alloc.</summary>
    /// <param name="state">Captured source + writer.</param>
    /// <param name="offset">Token offset.</param>
    /// <param name="length">Token length.</param>
    /// <param name="cls">Token classification.</param>
    private static void EmitFromState(EmitState state, int offset, int length, TokenClass cls) =>
        EmitToken(state.Source.AsSpan(offset, length), cls, state.Writer);

    /// <summary>Writes a single token, with its CSS class span when classified, escaping HTML metacharacters.</summary>
    /// <param name="text">Token text.</param>
    /// <param name="cls">Token classification.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitToken(ReadOnlySpan<char> text, TokenClass cls, IBufferWriter<byte> writer)
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

    /// <summary>Per-<see cref="Emit"/> state struct threaded through <see cref="Lexer.Tokenise{T}"/> so the callback can stay static.</summary>
    /// <param name="Source">Source string.</param>
    /// <param name="Writer">UTF-8 sink.</param>
    private readonly record struct EmitState(string Source, IBufferWriter<byte> Writer);
}
