// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Snippets;

/// <summary>
/// Snippets plugin. Expands lines of the form <c>--8&lt;-- "path/to/file.md"</c>
/// into the contents of the referenced file before any other markdown rewriter
/// runs, resolving each path against the configured base directories in order.
/// </summary>
public sealed class SnippetsPlugin : IPagePreRenderPlugin
{
    /// <summary>Resolution roots in order; the first hit wins.</summary>
    private readonly DirectoryPath[] _basePaths;

    /// <summary>Initializes a new instance of the <see cref="SnippetsPlugin"/> class with the given resolution roots.</summary>
    /// <param name="basePaths">Directories to resolve snippet paths against. Order is preserved on lookup.</param>
    public SnippetsPlugin(params DirectoryPath[] basePaths)
    {
        ArgumentNullException.ThrowIfNull(basePaths);
        _basePaths = basePaths;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "snippets"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => new(PluginBand.Early);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        source.IndexOf("--8<--"u8) >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        SnippetsRewriter.Rewrite(context.Source, context.Output, _basePaths);
}
