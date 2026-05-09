// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Snippets;

/// <summary>
/// Snippets plugin (pymdownx.snippets). Resolves <c>--8&lt;-- "file"</c> and
/// <c>--8&lt;-- "file#section"</c> includes inline at preprocess time, reading
/// from a configurable base directory (defaults to the build's docs root).
/// Section boundaries inside snippet files are marked with HTML comments
/// (<c>&lt;!-- @section name --&gt;</c> ... <c>&lt;!-- @endsection --&gt;</c>).
/// Recursive includes are bounded; missing files render as a fenced-code
/// error block.
/// </summary>
public sealed class SnippetsPlugin : IBuildConfigurePlugin, IPagePreRenderPlugin
{
    /// <summary>Optional override for the snippet base directory; empty means use the build's input root.</summary>
    private readonly DirectoryPath _baseDirectoryOverride;

    /// <summary>Resolved base directory captured at configure time.</summary>
    private DirectoryPath _baseDirectory;

    /// <summary>Build-scoped cache of resolved snippet bytes, keyed by include-path bytes.</summary>
    private Dictionary<byte[], byte[]> _fileCache = new(ByteArrayComparer.Instance);

    /// <summary>Initializes a new instance of the <see cref="SnippetsPlugin"/> class with no base-directory override (defaults to the build's docs root).</summary>
    public SnippetsPlugin()
        : this(default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SnippetsPlugin"/> class with a caller-supplied base directory.</summary>
    /// <param name="baseDirectory">Absolute path to the snippet root, or <see langword="default"/> to use the docs root.</param>
    public SnippetsPlugin(DirectoryPath baseDirectory) => _baseDirectoryOverride = baseDirectory;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "snippets"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _baseDirectory = _baseDirectoryOverride.IsEmpty ? context.InputRoot : _baseDirectoryOverride;
        _fileCache = new(ByteArrayComparer.Instance);
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => source.IndexOf("--8<--"u8) >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context)
    {
        var source = context.Source;
        var writer = context.Output;
        if (_baseDirectory.IsEmpty)
        {
            writer.Write(source);
            return;
        }

        SnippetsRewriter.Rewrite(source, _baseDirectory, _fileCache, writer);
    }
}
