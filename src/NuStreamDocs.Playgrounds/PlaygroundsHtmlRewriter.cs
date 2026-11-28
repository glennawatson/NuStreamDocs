// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Playgrounds;

/// <summary>Rewrites rendered <c>http-request</c> and <c>playground</c> fenced blocks when <c>SuperFencesPlugin</c> is absent.</summary>
internal static class PlaygroundsHtmlRewriter
{
    /// <summary>Gets the opening HTML prefix for <c>http-request</c> code blocks.</summary>
    private static ReadOnlySpan<byte> HttpFenceOpen => "<pre><code class=\"language-http-request\">"u8;

    /// <summary>Gets the opening HTML prefix for <c>playground</c> code blocks.</summary>
    private static ReadOnlySpan<byte> SandboxFenceOpen => "<pre><code class=\"language-playground\">"u8;

    /// <summary>Gets the closing HTML suffix for fenced code blocks.</summary>
    private static ReadOnlySpan<byte> FenceClose => "</code></pre>"u8;

    /// <summary>Checks whether <paramref name="html"/> contains an unrendered playground fence block.</summary>
    /// <param name="html">Rendered HTML bytes.</param>
    /// <returns><c>true</c> when at least one candidate block is present.</returns>
    internal static bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        html.IndexOf(HttpFenceOpen) >= 0 || html.IndexOf(SandboxFenceOpen) >= 0;

    /// <summary>Rewrites all <c>http-request</c> and <c>playground</c> blocks in <paramref name="html"/> into <paramref name="output"/>.</summary>
    /// <param name="html">Rendered HTML bytes.</param>
    /// <param name="enableTryIt">Whether HTTP blocks include the Try-It drawer.</param>
    /// <param name="output">Destination buffer.</param>
    internal static void Rewrite(ReadOnlySpan<byte> html, bool enableTryIt, IBufferWriter<byte> output)
    {
        var cursor = 0;
        while (cursor < html.Length)
        {
            if (!TryFindNextBlock(html[cursor..], out var relStart, out var isHttp, out var openLen))
            {
                output.Write(html[cursor..]);
                break;
            }

            output.Write(html.Slice(cursor, relStart));
            var bodyStart = cursor + relStart + openLen;
            var closeRel = html[bodyStart..].IndexOf(FenceClose);
            if (closeRel < 0)
            {
                output.Write(html[(cursor + relStart)..]);
                break;
            }

            var decoded = HttpRequestParser.DecodeHtmlEntities(html.Slice(bodyStart, closeRel));
            if (isHttp)
            {
                HttpRequestWidgetEmitter.Render(decoded, enableTryIt, output);
            }
            else
            {
                CodeSandboxWidgetEmitter.Render(decoded, output);
            }

            cursor = bodyStart + closeRel + FenceClose.Length;
        }
    }

    /// <summary>Finds whichever of <see cref="HttpFenceOpen"/> or <see cref="SandboxFenceOpen"/> occurs earliest in <paramref name="slice"/>.</summary>
    /// <param name="slice">Remaining HTML slice.</param>
    /// <param name="relStart">Relative offset of the match.</param>
    /// <param name="isHttp"><c>true</c> for <c>http-request</c>; <c>false</c> for <c>playground</c>.</param>
    /// <param name="openLength">Byte length of the matched opening sequence.</param>
    /// <returns><c>true</c> when a match was found.</returns>
    private static bool TryFindNextBlock(ReadOnlySpan<byte> slice, out int relStart, out bool isHttp, out int openLength)
    {
        var httpIdx = slice.IndexOf(HttpFenceOpen);
        var sandboxIdx = slice.IndexOf(SandboxFenceOpen);
        if (httpIdx < 0 && sandboxIdx < 0)
        {
            relStart = -1;
            isHttp = false;
            openLength = 0;
            return false;
        }

        if (httpIdx >= 0 && (sandboxIdx < 0 || httpIdx < sandboxIdx))
        {
            relStart = httpIdx;
            isHttp = true;
            openLength = HttpFenceOpen.Length;
            return true;
        }

        relStart = sandboxIdx;
        isHttp = false;
        openLength = SandboxFenceOpen.Length;
        return true;
    }
}
