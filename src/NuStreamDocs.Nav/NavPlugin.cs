// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Links;
using NuStreamDocs.Nav.Logging;
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
/// During <see cref="DiscoverAsync"/> the plugin scans the docs root,
/// builds the nav tree, and caches the URL index for per-page lookup.
/// During <see cref="PostRender"/> it replaces the marker
/// <c>&lt;!--@@nav@@--&gt;</c> a theme template emits with the rendered
/// nav for the current page; when <see cref="NavOptions.Prune"/> is on
/// only the active branch is emitted — matching mkdocs-material
/// <c>navigation.prune</c>.
/// </para>
/// </remarks>
public sealed class NavPlugin : IBuildDiscoverPlugin, IPagePostRenderPlugin, INavNeighboursProvider
{
    /// <summary>Length of the <c>.md</c> extension stripped when computing served URLs.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Bytes added when <c>.md</c> becomes <c>.html</c> (<c>.html</c> = 5, <c>.md</c> = 3).</summary>
    private const int HtmlExtensionGrowth = 2;

    /// <summary>Sentinel End value for a span that hasn't yet been patched by its enclosing section.</summary>
    private const int UnsetSpanEnd = -1;

    /// <summary>Length of one packed (Int64 ticks + Int64 length) stat tuple in the fingerprint scratch buffer.</summary>
    private const int StatTupleLength = 16;

    /// <summary>Byte offset of the length field inside the stat tuple.</summary>
    private const int StatLengthOffset = 8;

    /// <summary>Tiebreak that orders nav-marker substitution after the theme shell wrap (which uses the bare <see cref="PluginBand.Latest"/>) and after <c>TocPlugin</c> (tiebreak 1).</summary>
    private const int PostRenderTiebreak = 2;

    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly NavOptions _options;

    /// <summary>Logger handed to the tree builder.</summary>
    private readonly ILogger _logger;

    /// <summary>The build-time nav tree (class graph) constructed during <see cref="DiscoverAsync"/>; null until then. Held for diagnostics, neighbour linearization, and orphan reporting.</summary>
    private NavNode? _root;

    /// <summary>The flat render-time nav tree consumed by <see cref="NavRenderer"/>; null until <see cref="DiscoverAsync"/> runs.</summary>
    private NavTree? _tree;

    /// <summary>Stat-based fingerprint of the input tree from the previous build; <c>0</c> when not yet computed.</summary>
    private ulong _lastTreeFingerprint;

    /// <summary>True when the configured build emits directory-style served URLs.</summary>
    private bool _useDirectoryUrls;

    /// <summary>UTF-8 URL bytes → flat-tree node-index lookup; built once per discover so per-page renders resolve the active node in O(1) without re-encoding the page URL.</summary>
    private Dictionary<byte[], int>? _urlIndex;

    /// <summary>Linearized leaf-page nodes in nav order; built lazily on the first <see cref="GetNeighbours(FilePath)"/> call.</summary>
    private NavNode[]? _orderedLeaves;

    /// <summary>UTF-8 path bytes → index lookup over <see cref="_orderedLeaves"/>; built lazily alongside it.</summary>
    private Dictionary<byte[], int>? _leafIndex;

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

    /// <summary>Gets the UTF-8 marker bytes the theme places where the rendered nav should land.</summary>
    public static byte[] NavMarker { get; } = [.. "<!--@@nav@@-->"u8];

    /// <summary>Gets the UTF-8 marker bytes the theme places where the top-bar tabs should land (mkdocs-material's <c>navigation.tabs</c>).</summary>
    public static byte[] NavTabsMarker { get; } = [.. "<!--@@nav-tabs@@-->"u8];

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "nav"u8;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Early);

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <summary>Gets the computed nav tree root; null before <see cref="DiscoverAsync"/> has run.</summary>
    public object? Root => _root;

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var useDirectoryUrls = _options.UseDirectoryUrls ?? context.UseDirectoryUrls;

        if (_root is not null && _useDirectoryUrls == useDirectoryUrls && _options.CuratedEntries.Length is 0)
        {
            var fingerprint = ComputeTreeFingerprint(context.InputRoot);
            if (fingerprint == _lastTreeFingerprint)
            {
                NavLoggingHelper.LogNavRebuildSkipped(_logger);
                return ValueTask.CompletedTask;
            }

            _lastTreeFingerprint = fingerprint;
        }

        _useDirectoryUrls = useDirectoryUrls;
        _root = _options.CuratedEntries.Length > 0
            ? CuratedNavBuilder.Build(context.InputRoot, _options.CuratedEntries, useDirectoryUrls, _logger)
            : NavTreeBuilder.Build(context.InputRoot, in _options, useDirectoryUrls, _logger);
        _tree = NavTreeFlattener.Flatten(_root);
        _urlIndex = NavRenderer.BuildUrlIndex(_tree);
        _orderedLeaves = null;
        _leafIndex = null;
        _sectionSpans = null;

        if (_options.CuratedEntries.Length is 0)
        {
            _lastTreeFingerprint = ComputeTreeFingerprint(context.InputRoot);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html)
    {
        if (_tree is null)
        {
            return false;
        }

        return html.IndexOf("<!--@@nav@@-->"u8) >= 0 || (_options.Tabs && html.IndexOf("<!--@@nav-tabs@@-->"u8) >= 0);
    }

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        if (_tree is null)
        {
            context.Output.Write(context.Html);
            return;
        }

        var html = context.Html;
        var relativePath = context.RelativePath;
        var output = context.Output;

        // First-pass marker replacement: copy bytes through the writer, swapping
        // every recognized marker with its rendered payload.
        var cursor = 0;
        while (cursor < html.Length)
        {
            var remaining = html[cursor..];
            var navIdx = remaining.IndexOf("<!--@@nav@@-->"u8);
            var tabsIdx = _options.Tabs ? remaining.IndexOf("<!--@@nav-tabs@@-->"u8) : -1;

            if (navIdx < 0 && tabsIdx < 0)
            {
                Write(output, remaining);
                return;
            }

            // Pick whichever marker comes first.
            int markerOffset;
            int markerLength;
            bool isTabs;
            if (navIdx >= 0 && (tabsIdx < 0 || navIdx <= tabsIdx))
            {
                markerOffset = navIdx;
                markerLength = "<!--@@nav@@-->"u8.Length;
                isTabs = false;
            }
            else
            {
                markerOffset = tabsIdx;
                markerLength = "<!--@@nav-tabs@@-->"u8.Length;
                isTabs = true;
            }

            Write(output, remaining[..markerOffset]);
            if (isTabs)
            {
                RenderTabs(output, relativePath);
            }
            else
            {
                RenderNav(output, relativePath);
            }

            cursor += markerOffset + markerLength;
        }
    }

    /// <summary>Looks up the previous and next leaf pages for <paramref name="relativePath"/> in the nav's natural traversal order.</summary>
    /// <param name="relativePath">Source-relative path of the current page (e.g. <c>guide/intro.md</c>).</param>
    /// <returns>The neighbours; empty fields when there is no previous or no next, or <see cref="NavNeighbours.None"/> when the page is not in the nav.</returns>
    public NavNeighbours GetNeighbours(FilePath relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        if (IsRootIndex(relativePath) || !TryResolveIndex(relativePath, out var idx))
        {
            return NavNeighbours.None;
        }

        var ordered = _orderedLeaves!;
        return BoundedNeighbours(ordered, idx, 0, ordered.Length);
    }

    /// <inheritdoc/>
    public NavNeighbours GetSectionNeighbours(FilePath relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        if (IsRootIndex(relativePath) || !TryResolveIndex(relativePath, out var idx))
        {
            return NavNeighbours.None;
        }

        var (start, end) = _sectionSpans![idx];

        // Section of one: no real neighbours, suppress prev/next.
        return end - start <= 1 ? NavNeighbours.None : BoundedNeighbours(_orderedLeaves!, idx, start, end);
    }

    /// <inheritdoc/>
    public bool ShouldHidePrimarySidebar(FilePath relativePath)
    {
        if (relativePath.IsEmpty || _tree is null || _urlIndex is null)
        {
            return false;
        }

        // The docs-root index page is its own thing — landing pages still want the nav.
        if (IsRootIndex(relativePath))
        {
            return false;
        }

        var encoded = ServedUrlBytes.FromPath(relativePath, _useDirectoryUrls);
        if (!_urlIndex.TryGetValue(encoded, out var activeIdx))
        {
            return false;
        }

        var nodes = _tree.Nodes;
        var active = nodes[activeIdx];

        // Top-level leaf with no descendants: nothing the primary sidebar can usefully render.
        return active is { IsSection: false, ChildCount: 0 } && IsTopLevelChild(nodes, activeIdx);
    }

    /// <summary>Gets the typed nav root; for use from the plugin's own assembly + tests.</summary>
    /// <returns>The nav root, or null when <see cref="DiscoverAsync"/> has not yet run.</returns>
    internal NavNode? GetRoot() => _root;

    /// <summary>True when <paramref name="candidateIndex"/> sits in the root's direct-child range.</summary>
    /// <param name="nodes">Flat tree node array.</param>
    /// <param name="candidateIndex">Candidate node index.</param>
    /// <returns>True for top-level pages.</returns>
    private static bool IsTopLevelChild(NavTreeNode[] nodes, int candidateIndex)
    {
        var root = nodes[0];
        return candidateIndex >= root.FirstChildIndex && candidateIndex < root.FirstChildIndex + root.ChildCount;
    }

    /// <summary>Returns true when <paramref name="relativePath"/> is the docs-root <c>index.md</c>.</summary>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <returns>True for the root landing page.</returns>
    private static bool IsRootIndex(FilePath relativePath)
    {
        var value = relativePath.Value;
        return value is "index.md" or "index.MD" or "INDEX.md" or "INDEX.MD";
    }

    /// <summary>Computes a cheap stat-only fingerprint over every <c>.md</c> and <c>.pages</c> file under <paramref name="inputRoot"/>.</summary>
    /// <param name="inputRoot">Absolute docs root.</param>
    /// <returns>xxHash3 over sorted (path|ticks|length) tuples; <c>0</c> when the root is missing.</returns>
    private static ulong ComputeTreeFingerprint(DirectoryPath inputRoot)
    {
        if (!Directory.Exists(inputRoot))
        {
            return 0UL;
        }

        List<(string Path, long Ticks, long Length)> entries = new(capacity: 256);
        AppendStats(inputRoot, "*.md", entries);
        AppendStats(inputRoot, ".pages", entries);
        entries.Sort(static (a, b) => string.CompareOrdinal(a.Path, b.Path));

        XxHash3 hash = new();
        Span<byte> scratch = stackalloc byte[StatTupleLength];
        for (var i = 0; i < entries.Count; i++)
        {
            hash.Append(Encoding.UTF8.GetBytes(entries[i].Path));
            BinaryPrimitives.WriteInt64LittleEndian(scratch[..StatLengthOffset], entries[i].Ticks);
            BinaryPrimitives.WriteInt64LittleEndian(scratch[StatLengthOffset..], entries[i].Length);
            hash.Append(scratch);
        }

        return hash.GetCurrentHashAsUInt64();
    }

    /// <summary>Appends one stat tuple per matching file under <paramref name="root"/> to <paramref name="entries"/>.</summary>
    /// <param name="root">Absolute docs root.</param>
    /// <param name="searchPattern">Glob pattern (e.g. <c>*.md</c>).</param>
    /// <param name="entries">Accumulator.</param>
    private static void AppendStats(DirectoryPath root, string searchPattern, List<(string Path, long Ticks, long Length)> entries)
    {
        var files = Directory.GetFiles(root, searchPattern, SearchOption.AllDirectories);
        for (var i = 0; i < files.Length; i++)
        {
            FileInfo info = new(files[i]);
            entries.Add((files[i], info.LastWriteTimeUtc.Ticks, info.Length));
        }
    }

    /// <summary>Builds the linearized leaves, path index, and section spans for <paramref name="root"/> in one pass.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <returns>The three index pieces.</returns>
    private static (NavNode[] Leaves, Dictionary<byte[], int> Index, (int Start, int End)[] Spans) BuildIndex(NavNode root)
    {
        List<NavNode> leaves = [];
        List<(int Start, int End)> spans = [];
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
        Dictionary<byte[], int> index = new(leaves.Count, ByteArrayComparer.Instance);
        for (var i = 0; i < ordered.Length; i++)
        {
            index[Encoding.UTF8.GetBytes(ordered[i].RelativePath)] = i;
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
    /// <returns>Relative path or empty.</returns>
    private static FilePath PathOrEmpty(NavNode[] leaves, int idx) =>
        idx >= 0 && idx < leaves.Length ? leaves[idx].RelativePath : default;

    /// <summary>Returns the UTF-8 title bytes at <paramref name="idx"/> or empty when out of range.</summary>
    /// <param name="leaves">Leaf array.</param>
    /// <param name="idx">Candidate index.</param>
    /// <returns>Pre-encoded title bytes or empty.</returns>
    private static byte[] TitleOrEmpty(NavNode[] leaves, int idx) =>
        idx >= 0 && idx < leaves.Length ? leaves[idx].Title : [];

    /// <summary>Encodes <paramref name="relativePath"/> as the served-page URL bytes into <paramref name="destination"/>.</summary>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="useDirectoryUrls">True when the served site uses directory-style URLs.</param>
    /// <param name="destination">UTF-8 destination span sized for the worst-case served-path output.</param>
    /// <returns>Number of bytes written to <paramref name="destination"/>.</returns>
    private static int EncodePageUrlBytes(FilePath relativePath, bool useDirectoryUrls, Span<byte> destination)
    {
        var path = relativePath.Value;
        var endsWithMd = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var keepLength = endsWithMd ? path.Length - MarkdownExtensionLength : path.Length;
        var keptBytes = Encoding.UTF8.GetBytes(path.AsSpan(0, keepLength), destination);
        if (!endsWithMd)
        {
            return keptBytes;
        }

        if (!useDirectoryUrls)
        {
            ".html"u8.CopyTo(destination[keptBytes..]);
            return keptBytes + ".html"u8.Length;
        }

        var stem = path.AsSpan(0, keepLength);
        var lastSlash = stem.LastIndexOfAny('/', '\\');
        var fileName = lastSlash >= 0 ? stem[(lastSlash + 1)..] : stem;
        if (fileName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return lastSlash >= 0 ? Encoding.UTF8.GetBytes(stem[..(lastSlash + 1)], destination) : 0;
        }

        destination[keptBytes] = (byte)'/';
        return keptBytes + 1;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Lazily builds the leaf/index/span tables on first call and resolves <paramref name="relativePath"/> to its leaf index via a single UTF-8 encode + byte-keyed probe.</summary>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="idx">Resolved index; -1 when not in the nav.</param>
    /// <returns>True when found.</returns>
    private bool TryResolveIndex(FilePath relativePath, out int idx)
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

        var path = relativePath.Value;
        const int StackBufferLimit = 256;
        var maxBytes = Encoding.UTF8.GetMaxByteCount(path.Length);
        Span<byte> stackBuf = stackalloc byte[StackBufferLimit];
        var keyBuffer = maxBytes <= StackBufferLimit ? stackBuf : new byte[maxBytes];
        var written = Encoding.UTF8.GetBytes(path, keyBuffer);
        return _leafIndex!.TryGetValueByUtf8(keyBuffer[..written], out idx);
    }

    /// <summary>Renders the sidebar nav for the active page; switches between full and pruned trees per the configured options.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="relativePath">Source-relative path of the page being rendered.</param>
    private void RenderNav(IBufferWriter<byte> writer, FilePath relativePath)
    {
        var activeIdx = ResolveActiveIndex(relativePath);

        if (_options.Prune)
        {
            NavRenderer.RenderSidebarPruned(_tree!, activeIdx, writer);
            return;
        }

        NavRenderer.RenderSidebarFull(_tree!, activeIdx, writer);
    }

    /// <summary>Renders the top-level nav as a horizontal tab bar at the position of the page's <c>&lt;!--@@nav-tabs@@--&gt;</c> marker.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="relativePath">Source-relative path of the current page; resolves which tab is active.</param>
    private void RenderTabs(IBufferWriter<byte> writer, FilePath relativePath)
    {
        var activeIdx = ResolveActiveIndex(relativePath);
        NavRenderer.RenderTabs(_tree!, activeIdx, writer);
    }

    /// <summary>Encodes <paramref name="relativePath"/> as the served URL bytes and probes <see cref="_urlIndex"/> for the active node's flat-tree index.</summary>
    /// <param name="relativePath">Source-relative path of the page being rendered.</param>
    /// <returns>Active node index, or <c>-1</c> when the page is not in the nav.</returns>
    private int ResolveActiveIndex(FilePath relativePath)
    {
        const int StackUrlLimit = 256;
        var capacity = Encoding.UTF8.GetMaxByteCount(relativePath.Value.Length) + HtmlExtensionGrowth;
        Span<byte> stackBuf = stackalloc byte[StackUrlLimit];
        var urlBuffer = capacity <= StackUrlLimit ? stackBuf : new byte[capacity];
        var written = EncodePageUrlBytes(relativePath, _useDirectoryUrls, urlBuffer);
        return _urlIndex!.TryGetValueByUtf8(urlBuffer[..written], out var activeIdx) ? activeIdx : -1;
    }
}
