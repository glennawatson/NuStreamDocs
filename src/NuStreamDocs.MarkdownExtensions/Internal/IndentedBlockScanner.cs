// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.Internal;

/// <summary>
/// Shared block-scanning helpers for preprocessors that consume an
/// opener line followed by an indented body (admonitions, details,
/// tabbed). Stateless so every preprocessor in the assembly reuses
/// the same predicate set without duplicating the byte loop.
/// </summary>
internal static class IndentedBlockScanner
{
    /// <summary>Body lines must start with this many spaces (or a tab) to belong to the block.</summary>
    public const int BodyIndent = 4;

    /// <summary>Consumes indented body lines starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Byte offset just past the opener line.</param>
    /// <returns>The exclusive end of the block.</returns>
    public static int ConsumeBody(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        var lastContent = offset;
        while (p < source.Length)
        {
            var rel = source[p..].IndexOf((byte)'\n');
            var lineEnd = rel < 0 ? source.Length : p + rel + 1;

            if (IsBlankLine(source[p..lineEnd]))
            {
                p = lineEnd;
                continue;
            }

            if (!HasBodyIndent(source, p))
            {
                break;
            }

            lastContent = lineEnd;
            p = lineEnd;
        }

        return lastContent;
    }

    /// <summary>Returns true when <paramref name="line"/> contains only spaces, tabs, CR, or LF.</summary>
    /// <param name="line">UTF-8 line bytes including any trailing newline.</param>
    /// <returns>True when blank.</returns>
    public static bool IsBlankLine(ReadOnlySpan<byte> line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            var b = line[i];
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>Returns true when the line at <paramref name="offset"/> starts with at least <see cref="BodyIndent"/> spaces or a tab.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Offset of the line's first byte.</param>
    /// <returns>True when indented enough to belong to the body.</returns>
    public static bool HasBodyIndent(ReadOnlySpan<byte> source, int offset)
    {
        if (offset >= source.Length)
        {
            return false;
        }

        if (source[offset] == (byte)'\t')
        {
            return true;
        }

        var spaces = 0;
        while (offset + spaces < source.Length && source[offset + spaces] == (byte)' ' && spaces < BodyIndent)
        {
            spaces++;
        }

        return spaces >= BodyIndent;
    }

    /// <summary>Strips <see cref="BodyIndent"/> columns of leading indentation from each line and writes the rest to <paramref name="writer"/>.</summary>
    /// <param name="body">UTF-8 body bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void WriteDeindented(ReadOnlySpan<byte> body, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < body.Length)
        {
            var rel = body[i..].IndexOf((byte)'\n');
            var lineEnd = rel < 0 ? body.Length : i + rel + 1;
            var line = body[i..lineEnd];

            if (IsBlankLine(line))
            {
                writer.Write(line);
                i = lineEnd;
                continue;
            }

            if (line.Length > 0 && line[0] == (byte)'\t')
            {
                writer.Write(line[1..]);
            }
            else
            {
                var skip = 0;
                while (skip < BodyIndent && skip < line.Length && line[skip] == (byte)' ')
                {
                    skip++;
                }

                writer.Write(line[skip..]);
            }

            i = lineEnd;
        }
    }
}
