// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Per-page context handed to <see cref="IPageScanPlugin.Scan"/>.
/// </summary>
/// <remarks>
/// Read-only view of the post-render HTML. Scan plugins extract typed
/// facts (heading IDs, search documents, feed entries) and publish into
/// shared registries owned by their plugin instance. Scan must not
/// mutate page bytes — that's the job of <see cref="IPagePostRenderPlugin"/>
/// (before the cross-page barrier) or <see cref="IPagePostResolvePlugin"/>
/// (after the cross-page barrier).
/// </remarks>
public readonly ref struct PageScanContext
{
    /// <summary>Initializes a new instance of the <see cref="PageScanContext"/> struct.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="source">Original UTF-8 markdown bytes.</param>
    /// <param name="html">UTF-8 HTML bytes (post-render, pre-resolve).</param>
    public PageScanContext(FilePath relativePath, ReadOnlySpan<byte> source, ReadOnlySpan<byte> html)
    {
        RelativePath = relativePath;
        Source = source;
        Html = html;
    }

    /// <summary>Gets the page path relative to the input root.</summary>
    public FilePath RelativePath { get; }

    /// <summary>Gets the original UTF-8 markdown bytes.</summary>
    public ReadOnlySpan<byte> Source { get; }

    /// <summary>Gets the UTF-8 HTML bytes to be scanned (read-only).</summary>
    public ReadOnlySpan<byte> Html { get; }
}
