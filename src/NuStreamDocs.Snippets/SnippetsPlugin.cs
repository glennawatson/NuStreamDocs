// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Snippets;

/// <summary>
/// Snippets plugin (pymdownx.snippets). Resolves
/// <c>--8&lt;-- "file"</c> includes inline at preprocess time,
/// reading from a configurable base directory (defaults to the
/// build's docs root).
/// </summary>
/// <remarks>
/// Whole-file includes (<c>--8&lt;-- "file"</c>) and section includes
/// (<c>--8&lt;-- "file#section"</c>) both ship. Section boundaries inside
/// snippet files are marked with HTML comments —
/// <c>&lt;!-- @section name --&gt;</c> opens, <c>&lt;!-- @endsection --&gt;</c>
/// closes — chosen over a sigil syntax because the comments are invisible
/// in any CommonMark renderer even when the snippets plugin is not in the
/// pipeline.
/// <para>
/// Recursive includes are supported up to a fixed depth (a guard
/// against include cycles). Missing files render as a fenced-code
/// error block so the build doesn't silently swallow typos.
/// </para>
/// </remarks>
public sealed class SnippetsPlugin : IBuildConfigurePlugin, IPagePreRenderPlugin
{
    /// <summary>Optional override for the snippet base directory; empty means use <c>InputRoot</c>.</summary>
    private readonly DirectoryPath _baseDirectoryOverride;

    /// <summary>Resolved base directory captured at <see cref="ConfigureAsync"/> time.</summary>
    private DirectoryPath _baseDirectory;

    /// <summary>Build-scoped cache of resolved snippet bytes, keyed by the UTF-8 include-path bytes lifted directly from the source span (no <see cref="string"/> round-trip).</summary>
    /// <remarks>
    /// Reset on every <see cref="ConfigureAsync"/> call so a watch-mode rebuild that reconfigures the plugin
    /// drops every cached file before re-running. Lifetime is bounded by the plugin instance — the host already
    /// disposes plugins between builds, so the cache cannot outlive a build context. We deliberately do NOT cache
    /// by file mtime: the plugin trusts its host to reconfigure when content changes, and a stat-per-lookup would
    /// negate the cache win on cold pages.
    /// </remarks>
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
