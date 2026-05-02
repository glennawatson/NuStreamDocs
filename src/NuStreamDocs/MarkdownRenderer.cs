// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Html;
using NuStreamDocs.Markdown;
using NuStreamDocs.Yaml;

namespace NuStreamDocs;

/// <summary>
/// Façade that runs <see cref="BlockScanner"/> followed by <see cref="HtmlEmitter"/>.
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>Initial capacity for the block buffer; sized for a typical page (~32 blocks) so the writer rarely re-grows.</summary>
    private const int InitialBlockCapacity = 32;

    /// <summary>Cap above which a parked block buffer is dropped instead of cached, so an outlier page doesn't pin a multi-MB array.</summary>
    private const int MaxCachedBlockCapacity = 4 * 1024;

    /// <summary>Per-thread parked block buffer reused across <see cref="Render"/> calls on the same worker.</summary>
    [ThreadStatic]
    private static ArrayBufferWriter<BlockSpan>? _blockBufferCache;

    /// <summary>
    /// Renders <paramref name="markdown"/> as UTF-8 HTML into <paramref name="writer"/>.
    /// </summary>
    /// <param name="markdown">UTF-8 markdown source.</param>
    /// <param name="writer">UTF-8 sink for the rendered HTML.</param>
    public static void Render(ReadOnlySpan<byte> markdown, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Skip past any leading YAML frontmatter so the `---` opener and
        // its key-value lines don't render as a thematic break + paragraphs.
        // Frontmatter is contract-level metadata for the build pipeline; it
        // should never reach the block scanner.
        if (YamlByteScanner.TryFindFrontmatter(markdown, out _, out var bodyStart))
        {
            markdown = markdown[bodyStart..];
        }

        var blockBuffer = _blockBufferCache;
        _blockBufferCache = null;
        if (blockBuffer is null)
        {
            blockBuffer = new(InitialBlockCapacity);
        }
        else
        {
            blockBuffer.ResetWrittenCount();
        }

        try
        {
            BlockScanner.Scan(markdown, blockBuffer);
            HtmlEmitter.Emit(markdown, blockBuffer.WrittenSpan, writer);
        }
        finally
        {
            if (blockBuffer.Capacity <= MaxCachedBlockCapacity)
            {
                _blockBufferCache = blockBuffer;
            }
        }
    }
}
