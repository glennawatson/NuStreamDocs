// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Nav;

/// <summary>
/// mkdocs-nav-equivalent plugin.
/// </summary>
/// <remarks>
/// Outer-layer instance type: holds the per-build option set and the
/// computed <see cref="NavNode"/> root. All tree-walking and ordering
/// is delegated to <see cref="NavTreeBuilder"/>'s static methods.
/// Plugin instances are created either via the parameterless ctor
/// (<c>builder.UsePlugin&lt;NavPlugin&gt;()</c>) or via the
/// extension method <c>builder.UseNav(...)</c> which captures
/// non-default options.
/// <para>
/// During <see cref="OnRenderPageAsync"/> the plugin scans the rendered
/// HTML for the marker <c>&lt;!--@@nav@@--&gt;</c> a theme template
/// emits, replaces it with the rendered nav for the current page, and
/// (when <see cref="NavOptions.Prune"/> is on) only emits the active
/// branch — matching mkdocs-material <c>navigation.prune</c>.
/// </para>
/// </remarks>
public sealed class NavPlugin : IDocPlugin, INavNeighboursProvider
{
    /// <summary>Length of the <c>.md</c> extension stripped when computing served URLs.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Sentinel End value for a span that hasn't yet been patched by its enclosing section.</summary>
    private const int UnsetSpanEnd = -1;

    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly NavOptions _options;

    /// <summary>Logger handed to the tree builder.</summary>
    private readonly ILogger _logger;

    /// <summary>The nav tree built during <see cref="OnConfigureAsync"/>; null until then.</summary>
    private NavNode? _root;

    /// <summary>URL → node lookup over the rendered tree; built once when the tree is built so per-page renders resolve the active node in O(1).</summary>
    private Dictionary<string, NavNode>? _urlIndex;

    /// <summary>Linearized leaf-page nodes in nav order; built lazily on the first <see cref="GetNeighbours(string)"/> call.</summary>
    private NavNode[]? _orderedLeaves;

    /// <summary>Path → index lookup over <see cref="_orderedLeaves"/>; built lazily alongside it.</summary>
    private Dictionary<string, int>? _leafIndex;

    /// <summary>Per-leaf <c>[sectionStart, sectionEndExclusive)</c> spans over <see cref="_orderedLeaves"/>; built lazily alongside it.</summary>
    private (int Start, int End)[]? _sectionSpans;

    /// <summary>Initializes a new instance of the <see cref="NavPlugin"/> class with default options.</summary>
    public NavPlugin()
        : this(NavOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public NavPlugin(in NavOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NavPlugin"/> class with a logger.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger.</param>
    public NavPlugin(in NavOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <summary>Gets the marker the theme places where the rendered nav should land.</summary>
    public static string NavMarker => "<!--@@nav@@-->";

    /// <inheritdoc/>
    public string Name => "nav";

    /// <summary>Gets the computed nav tree root; null before <see cref="OnConfigureAsync"/> has run.</summary>
    public object? Root => _root;

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _root = NavTreeBuilder.Build(context.InputRoot, in _options, _logger);
        _urlIndex = NavRenderer.BuildUrlIndex(_root);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_root is null)
        {
            return ValueTask.CompletedTask;
        }

        var html = context.Html;
        var written = html.WrittenSpan;
        var markerBytes = "<!--@@nav@@-->"u8;
        var markerIndex = written.IndexOf(markerBytes);
        if (markerIndex < 0)
        {
            return ValueTask.CompletedTask;
        }

        // Pooled snapshot: the rewrite reads from the snapshot while writing back into
        // html, so we need a separate buffer for the read side. Renting from ArrayPool
        // avoids a per-page byte[] alloc; the nav payload is bounded by the active
        // branch (single-digit kilobytes even on 13K-page sites with prune on).
        var length = written.Length;
        var rental = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            written.CopyTo(rental);
            html.ResetWrittenCount();

            var prefix = rental.AsSpan(0, markerIndex);
            var suffix = rental.AsSpan(markerIndex + markerBytes.Length, length - markerIndex - markerBytes.Length);

            Write(html, prefix);
            RenderNav(html, ToPageUrl(context.RelativePath));
            Write(html, suffix);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rental);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>Sitemap emission lands alongside the offline + feed plugins.</remarks>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <summary>Looks up the previous and next leaf pages for <paramref name="relativePath"/> in the nav's natural traversal order.</summary>
    /// <param name="relativePath">Source-relative path of the current page (e.g. <c>guide/intro.md</c>).</param>
    /// <returns>The neighbours; empty fields when there is no previous or no next, or <see cref="NavNeighbours.None"/> when the page is not in the nav.</returns>
    public NavNeighbours GetNeighbours(string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        if (!TryResolveIndex(relativePath, out var idx))
        {
            return NavNeighbours.None;
        }

        var ordered = _orderedLeaves!;
        return BoundedNeighbours(ordered, idx, 0, ordered.Length);
    }

    /// <inheritdoc/>
    public NavNeighbours GetSectionNeighbours(string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        if (!TryResolveIndex(relativePath, out var idx))
        {
            return NavNeighbours.None;
        }

        var (start, end) = _sectionSpans![idx];
        return BoundedNeighbours(_orderedLeaves!, idx, start, end);
    }

    /// <summary>Gets the typed nav root; for use from the plugin's own assembly + tests.</summary>
    /// <returns>The nav root, or null when <see cref="OnConfigureAsync"/> has not yet run.</returns>
    internal NavNode? GetRoot() => _root;

    /// <summary>Builds the linearized leaves, path index, and section spans for <paramref name="root"/> in one pass.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <returns>The three index pieces.</returns>
    private static (NavNode[] Leaves, Dictionary<string, int> Index, (int Start, int End)[] Spans) BuildIndex(NavNode root)
    {
        var leaves = new List<NavNode>();
        var spans = new List<(int Start, int End)>();
        Linearize(root, leaves, spans, 0);

        // Any leaf that bubbled all the way up to the root section is still
        // sentinel-marked; bind it to the root's full span.
        for (var i = 0; i < spans.Count; i++)
        {
            if (spans[i].End == UnsetSpanEnd)
            {
                spans[i] = (spans[i].Start, leaves.Count);
            }
        }

        NavNode[] ordered = [.. leaves];
        var index = new Dictionary<string, int>(leaves.Count, StringComparer.Ordinal);
        for (var i = 0; i < ordered.Length; i++)
        {
            index[ordered[i].RelativePath] = i;
        }

        return (ordered, index, [.. spans]);
    }

    /// <summary>Pre-order walk over <paramref name="node"/>.</summary>
    /// <param name="node">Current node.</param>
    /// <param name="leaves">Leaf accumulator.</param>
    /// <param name="spans">Per-leaf enclosing-section span accumulator.</param>
    /// <param name="enclosingStart">Start index of the closest enclosing section.</param>
    private static void Linearize(
        NavNode node,
        List<NavNode> leaves,
        List<(int Start, int End)> spans,
        int enclosingStart)
    {
        if (!node.IsSection)
        {
            // End is patched by the closest section that contains this leaf when its
            // recursion completes. Sentinel until then.
            leaves.Add(node);
            spans.Add((enclosingStart, UnsetSpanEnd));
            return;
        }

        if (!string.IsNullOrEmpty(node.IndexPath))
        {
            leaves.Add(new(node.Title, node.IndexPath, isSection: false, []));
            spans.Add((enclosingStart, UnsetSpanEnd));
        }

        var thisStart = leaves.Count;
        for (var i = 0; i < node.Children.Length; i++)
        {
            Linearize(node.Children[i], leaves, spans, thisStart);
        }

        var thisEnd = leaves.Count;

        // Patch only spans inside our window that haven't been claimed by an inner
        // section yet. Inner sections close first, so their leaves already have a
        // concrete End and we leave them alone.
        for (var i = thisStart; i < thisEnd; i++)
        {
            if (spans[i].End == UnsetSpanEnd)
            {
                spans[i] = (thisStart, thisEnd);
            }
        }
    }

    /// <summary>Returns prev/next neighbours of <paramref name="idx"/>, treating <c>[start, end)</c> as the bounding window.</summary>
    /// <param name="leaves">Leaf array.</param>
    /// <param name="idx">Index of the current page.</param>
    /// <param name="start">Window start (inclusive).</param>
    /// <param name="end">Window end (exclusive).</param>
    /// <returns>The bounded neighbours; empty fields when there is no neighbour on a side.</returns>
    private static NavNeighbours BoundedNeighbours(NavNode[] leaves, int idx, int start, int end)
    {
        var prevIdx = idx - 1 >= start ? idx - 1 : -1;
        var nextIdx = idx + 1 < end ? idx + 1 : -1;
        return new(
            PathOrEmpty(leaves, prevIdx),
            TitleOrEmpty(leaves, prevIdx),
            PathOrEmpty(leaves, nextIdx),
            TitleOrEmpty(leaves, nextIdx));
    }

    /// <summary>Returns the relative path at <paramref name="idx"/> or empty when out of range.</summary>
    /// <param name="leaves">Leaf array.</param>
    /// <param name="idx">Candidate index.</param>
    /// <returns>Relative path or empty string.</returns>
    private static string PathOrEmpty(NavNode[] leaves, int idx) =>
        idx >= 0 && idx < leaves.Length ? leaves[idx].RelativePath : string.Empty;

    /// <summary>Returns the title at <paramref name="idx"/> or empty when out of range.</summary>
    /// <param name="leaves">Leaf array.</param>
    /// <param name="idx">Candidate index.</param>
    /// <returns>Title or empty string.</returns>
    private static string TitleOrEmpty(NavNode[] leaves, int idx) =>
        idx >= 0 && idx < leaves.Length ? leaves[idx].Title : string.Empty;

    /// <summary>Translates the source-relative markdown path to the served-page URL.</summary>
    /// <param name="relativePath">Source-relative path.</param>
    /// <returns>Site-relative URL.</returns>
    private static string ToPageUrl(string relativePath) =>
        relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? $"{relativePath.AsSpan(0, relativePath.Length - MarkdownExtensionLength)}.html"
            : relativePath;

    /// <summary>Bulk-writes <paramref name="bytes"/> into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink — concrete <see cref="ArrayBufferWriter{T}"/> so the JIT keeps the call site direct.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Lazily builds the leaf/index/span tables on first call and resolves <paramref name="relativePath"/> to its leaf index.</summary>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="idx">Resolved index; -1 when not in the nav.</param>
    /// <returns>True when found.</returns>
    private bool TryResolveIndex(string relativePath, out int idx)
    {
        idx = -1;
        if (_root is null)
        {
            return false;
        }

        if (_orderedLeaves is null)
        {
            (_orderedLeaves, _leafIndex, _sectionSpans) = BuildIndex(_root);
        }

        return _leafIndex!.TryGetValue(relativePath, out idx);
    }

    /// <summary>Renders the nav into <paramref name="writer"/> using the configured prune mode.</summary>
    /// <param name="writer">UTF-8 sink — concrete <see cref="ArrayBufferWriter{T}"/> so the JIT keeps the call site direct.</param>
    /// <param name="pageUrl">Page-relative URL of the page being rendered.</param>
    private void RenderNav(ArrayBufferWriter<byte> writer, string pageUrl)
    {
        // O(1) URL → node lookup against the index built once at configure time.
        _ = _urlIndex!.TryGetValue(pageUrl, out var activeNode);

        if (_options.Prune)
        {
            NavRenderer.RenderPruned(_root!, activeNode, writer);
            return;
        }

        NavRenderer.RenderFull(_root!, activeNode, writer);
    }
}
