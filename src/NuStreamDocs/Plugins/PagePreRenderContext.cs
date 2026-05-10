// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Per-page context handed to <see cref="IPagePreRenderPlugin.PreRender"/>. <see cref="Source"/>
/// is the raw markdown for the first preprocessor and the prior preprocessor's output for
/// subsequent ones.
/// </summary>
public readonly ref struct PagePreRenderContext
{
    /// <summary>Initializes a new instance of the <see cref="PagePreRenderContext"/> struct.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="output">UTF-8 sink the rewritten markdown is written into.</param>
    public PagePreRenderContext(in FilePath relativePath, ReadOnlySpan<byte> source, IBufferWriter<byte> output)
    {
        RelativePath = relativePath;
        Source = source;
        Output = output;
    }

    /// <summary>Gets the page path relative to the input root, e.g. <c>guide/intro.md</c>.</summary>
    public FilePath RelativePath { get; }

    /// <summary>Gets the UTF-8 markdown bytes to be rewritten.</summary>
    public ReadOnlySpan<byte> Source { get; }

    /// <summary>Gets the UTF-8 sink the rewritten markdown is written into.</summary>
    public IBufferWriter<byte> Output { get; }
}
