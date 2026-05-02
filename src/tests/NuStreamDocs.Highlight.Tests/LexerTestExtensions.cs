// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Test-only extensions sharing the lex+emit-and-decode flow across the lexer tests.</summary>
internal static class LexerTestExtensions
{
    /// <summary>Tokenises <paramref name="source"/> through <paramref name="lexer"/>, emits the classed HTML, and decodes the UTF-8 result back to a <see cref="string"/> for assertions.</summary>
    /// <param name="lexer">Configured lexer.</param>
    /// <param name="source">Source as UTF-8 bytes.</param>
    /// <returns>Rendered HTML.</returns>
    public static string Render(this Lexer lexer, ReadOnlySpan<byte> source)
    {
        var sink = new ArrayBufferWriter<byte>();
        HighlightEmitter.Emit(lexer, source.ToArray(), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
