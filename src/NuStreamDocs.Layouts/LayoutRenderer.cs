// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Layouts.Logging;

namespace NuStreamDocs.Layouts;

/// <summary>Executes a tokenized layout template against a <see cref="LayoutContext"/> and writes the result to an <see cref="IBufferWriter{T}"/>.</summary>
internal static class LayoutRenderer
{
    /// <summary>Length of the literal <c>"page."</c> prefix used when constructing missing-variable diagnostics.</summary>
    private const string PagePrefix = "page.";

    /// <summary>Renders <paramref name="templateName"/> against <paramref name="context"/> and writes the result to <paramref name="writer"/>.</summary>
    /// <param name="templateName">UTF-8 layout file name (e.g. <c>"page.html"u8</c>).</param>
    /// <param name="templateDirectory">Directory layouts are loaded from.</param>
    /// <param name="context">Variable bag.</param>
    /// <param name="maxDepth">Recursion cap on <c>{% include %}</c> / <c>{% extends %}</c> expansion.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="logger">Logger for warnings.</param>
    /// <param name="cache">
    /// Optional per-build template cache; when <see langword="null"/> every load round-trips the disk and re-parses
    /// (used by tests and one-shot callers that don't own a plugin instance).
    /// </param>
    /// <returns>True when the template was loaded and rendered; false when the template file was missing.</returns>
    public static bool Render(
        ReadOnlySpan<byte> templateName,
        DirectoryPath templateDirectory,
        LayoutContext context,
        int maxDepth,
        IBufferWriter<byte> writer,
        ILogger logger,
        TemplateCache? cache)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(logger);

        if (!TryLoadTemplate(templateName, templateDirectory, cache, out var template, out var resolvedPath))
        {
            LayoutsLoggingHelper.LogMissingTemplate(logger, resolvedPath);
            return false;
        }

        RenderState state = new(templateDirectory, context, maxDepth, writer, logger, cache);

        if (TryFindExtends(template, out var parentTarget))
        {
            RenderWithInheritance(parentTarget, template, state);
            return true;
        }

        RenderTokens(template, state, blocks: null, currentBlock: null, depth: 0);
        return true;
    }

    /// <summary>Loads a template file, trying both the literal name and a <c>.html</c>-appended variant; consults <paramref name="cache"/> when supplied.</summary>
    /// <param name="templateName">UTF-8 layout file name.</param>
    /// <param name="templateDirectory">Resolution root.</param>
    /// <param name="cache">Optional per-build cache. On hit the parsed unit is returned without touching disk; on miss the loaded + parsed unit is written back.</param>
    /// <param name="template">Parsed template on success.</param>
    /// <param name="resolvedPath">Last attempted absolute path (for diagnostics).</param>
    /// <returns>True when the file was loaded.</returns>
    private static bool TryLoadTemplate(ReadOnlySpan<byte> templateName, DirectoryPath templateDirectory, TemplateCache? cache, out TemplateUnit template, out string resolvedPath)
    {
        template = default;
        resolvedPath = string.Empty;
        if (templateName.IsEmpty || templateDirectory.IsEmpty)
        {
            return false;
        }

        if (cache is not null && cache.TryGet(templateName, out var hit))
        {
            template = hit.Unit;
            resolvedPath = hit.ResolvedPath.Value ?? string.Empty;
            return true;
        }

        return TryLoadFromDisk(templateName, templateDirectory, cache, out template, out resolvedPath);
    }

    /// <summary>Disk-only branch of <see cref="TryLoadTemplate"/> — runs only on cache miss.</summary>
    /// <param name="templateName">UTF-8 layout file name.</param>
    /// <param name="templateDirectory">Resolution root.</param>
    /// <param name="cache">Optional per-build cache.</param>
    /// <param name="template">Parsed template on success.</param>
    /// <param name="resolvedPath">Last attempted absolute path.</param>
    /// <returns>True when the file was found and parsed.</returns>
    private static bool TryLoadFromDisk(ReadOnlySpan<byte> templateName, DirectoryPath templateDirectory, TemplateCache? cache, out TemplateUnit template, out string resolvedPath)
    {
        template = default;
        var nameString = Encoding.UTF8.GetString(templateName);
        var path = Path.GetFullPath(nameString, templateDirectory.Value);
        resolvedPath = path;
        if (File.Exists(path))
        {
            template = TemplateUnit.From(File.ReadAllBytes(path));
            cache?.Add(templateName.ToArray(), new(template, new(path)));
            return true;
        }

        if (nameString.AsSpan().EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var altPath = Path.GetFullPath($"{nameString}.html", templateDirectory.Value);
        resolvedPath = altPath;
        if (!File.Exists(altPath))
        {
            return false;
        }

        template = TemplateUnit.From(File.ReadAllBytes(altPath));
        cache?.Add(templateName.ToArray(), new(template, new(altPath)));
        return true;
    }

    /// <summary>Searches <paramref name="template"/> for a leading <see cref="LayoutTokenKind.Extends"/>.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="parentTarget">Resolved parent template name on success.</param>
    /// <returns>True when the first non-literal-only-whitespace token is an extends.</returns>
    private static bool TryFindExtends(TemplateUnit template, out byte[] parentTarget)
    {
        parentTarget = [];
        var tokens = template.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Kind is LayoutTokenKind.Literal)
            {
                if (!IsAllWhitespace(template.Bytes.AsSpan(t.Start, t.End - t.Start)))
                {
                    return false;
                }

                continue;
            }

            if (t.Kind is LayoutTokenKind.Extends)
            {
                parentTarget = template.Bytes[t.PayloadStart..t.PayloadEnd];
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>Renders the parent template with the child's blocks overriding the parent's same-named blocks.</summary>
    /// <param name="parentTarget">UTF-8 parent template name.</param>
    /// <param name="child">Child template.</param>
    /// <param name="state">Render state.</param>
    private static void RenderWithInheritance(byte[] parentTarget, TemplateUnit child, RenderState state)
    {
        if (!TryLoadTemplate(parentTarget, state.TemplateDirectory, state.Cache, out var parent, out var parentPath))
        {
            LayoutsLoggingHelper.LogMissingTemplate(state.Logger, parentPath);

            // Fall back: render the child directly (its blocks become the output).
            RenderTokens(child, state, blocks: null, currentBlock: null, depth: 0);
            return;
        }

        Dictionary<byte[], BlockRange> childBlocks = new(ByteArrayComparer.Instance);
        CollectBlocks(child, childBlocks);

        BlockOverlay overlay = new(childBlocks, child);
        RenderTokens(parent, state, overlay, currentBlock: null, depth: 0);
    }

    /// <summary>Walks <paramref name="template"/> and records the token-index range of each top-level <c>{% block name %}</c>.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="blocks">Sink dictionary, name → block range.</param>
    private static void CollectBlocks(TemplateUnit template, Dictionary<byte[], BlockRange> blocks)
    {
        var tokens = template.Tokens;
        var i = 0;
        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Kind is not LayoutTokenKind.BlockOpen)
            {
                i++;
                continue;
            }

            var name = template.Bytes[t.PayloadStart..t.PayloadEnd];
            var (innerStart, innerEnd, closeIndex) = FindBlockEnd(tokens, i);
            blocks[name] = new(innerStart, innerEnd);
            i = closeIndex + 1;
        }
    }

    /// <summary>Renders the entire token stream of <paramref name="template"/>.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="state">Render state.</param>
    /// <param name="blocks">Optional child-block overlay.</param>
    /// <param name="currentBlock">Block currently being rendered (drives <c>{{ super() }}</c>).</param>
    /// <param name="depth">Current include depth.</param>
    private static void RenderTokens(TemplateUnit template, RenderState state, BlockOverlay? blocks, byte[]? currentBlock, int depth) =>
        RenderRange(template, new(0, template.Tokens.Count), state, blocks, currentBlock, depth);

    /// <summary>Renders a token-index range from <paramref name="template"/>.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="range">Inclusive-exclusive token range.</param>
    /// <param name="state">Render state.</param>
    /// <param name="blocks">Block overlay.</param>
    /// <param name="currentBlock">Current block name.</param>
    /// <param name="depth">Current include depth.</param>
    private static void RenderRange(TemplateUnit template, BlockRange range, RenderState state, BlockOverlay? blocks, byte[]? currentBlock, int depth)
    {
        var i = range.Start;
        while (i < range.End)
        {
            var consumed = RenderOne(template, i, state, blocks, currentBlock, depth);
            i = consumed + 1;
        }
    }

    /// <summary>Renders one token (or, for <c>{% block %}</c>, a whole nested range) and returns the index of the last token consumed.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="i">Current token index.</param>
    /// <param name="state">Render state.</param>
    /// <param name="blocks">Block overlay.</param>
    /// <param name="currentBlock">Block currently being rendered.</param>
    /// <param name="depth">Current include depth.</param>
    /// <returns>Index of the last token consumed by this call.</returns>
    private static int RenderOne(TemplateUnit template, int i, RenderState state, BlockOverlay? blocks, byte[]? currentBlock, int depth)
    {
        var t = template.Tokens[i];
        return t.Kind switch
        {
            LayoutTokenKind.Literal => EmitLiteral(template, t, state),
            LayoutTokenKind.Variable => EmitVariable(template, t, state),
            LayoutTokenKind.Super => EmitSuper(currentBlock, blocks, state, depth, i),
            LayoutTokenKind.Include => EmitInclude(template, t, state, blocks, depth, i),
            LayoutTokenKind.BlockOpen => RenderBlock(template, i, state, blocks, depth),
            LayoutTokenKind.Unsupported => EmitUnsupported(template, t, state, i),
            LayoutTokenKind.Malformed => EmitLiteral(template, t, state),
            _ => i,
        };
    }

    /// <summary>Copies a literal token's bytes to the writer.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Token.</param>
    /// <param name="state">Render state.</param>
    /// <returns>The token's own index; the caller advances past it.</returns>
    private static int EmitLiteral(TemplateUnit template, LayoutToken t, RenderState state)
    {
        Write(state.Writer, template.Bytes.AsSpan(t.Start, t.End - t.Start));
        return IndexOf(template, t);
    }

    /// <summary>Emits a variable's value (or empty + warning when missing).</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Variable token.</param>
    /// <param name="state">Render state.</param>
    /// <returns>The token's own index.</returns>
    private static int EmitVariable(TemplateUnit template, LayoutToken t, RenderState state)
    {
        var bareName = template.Bytes.AsSpan(t.PayloadStart, t.PayloadEnd - t.PayloadStart);
        if (state.Context.TryGetValue(bareName, out var value))
        {
            Write(state.Writer, value);
        }
        else
        {
            LayoutsLoggingHelper.LogMissingVariable(state.Logger, PagePrefix + Encoding.UTF8.GetString(bareName));
        }

        return IndexOf(template, t);
    }

    /// <summary>Emits the parent block's content at the current <c>{{ super() }}</c> position.</summary>
    /// <param name="currentBlock">Current block name.</param>
    /// <param name="blocks">Block overlay.</param>
    /// <param name="state">Render state.</param>
    /// <param name="depth">Current include depth.</param>
    /// <param name="tokenIndex">Index of the super token (returned as the consumed index).</param>
    /// <returns><paramref name="tokenIndex"/>.</returns>
    private static int EmitSuper(byte[]? currentBlock, BlockOverlay? blocks, RenderState state, int depth, int tokenIndex)
    {
        if (currentBlock is null || blocks is null || !blocks.TryGetParentRange(currentBlock, out var entry))
        {
            LayoutsLoggingHelper.LogSuperOutsideBlock(state.Logger);
            return tokenIndex;
        }

        RenderRange(entry.Template, entry.Range, state, blocks, currentBlock: null, depth);
        return tokenIndex;
    }

    /// <summary>Splices an include target into the writer.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Include token.</param>
    /// <param name="state">Render state.</param>
    /// <param name="blocks">Block overlay.</param>
    /// <param name="depth">Current include depth.</param>
    /// <param name="tokenIndex">Index of the include token (returned as the consumed index).</param>
    /// <returns><paramref name="tokenIndex"/>.</returns>
    private static int EmitInclude(TemplateUnit template, LayoutToken t, RenderState state, BlockOverlay? blocks, int depth, int tokenIndex)
    {
        var includeName = template.Bytes.AsSpan(t.PayloadStart, t.PayloadEnd - t.PayloadStart);
        if (depth >= state.MaxDepth)
        {
            LayoutsLoggingHelper.LogIncludeDepthExceeded(state.Logger, state.MaxDepth, Encoding.UTF8.GetString(includeName));
            return tokenIndex;
        }

        if (!TryLoadTemplate(includeName, state.TemplateDirectory, state.Cache, out var included, out var includePath))
        {
            LayoutsLoggingHelper.LogMissingInclude(state.Logger, includePath);
            return tokenIndex;
        }

        RenderTokens(included, state, blocks, currentBlock: null, depth + 1);
        return tokenIndex;
    }

    /// <summary>Logs an unsupported tag and copies its source bytes through.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Unsupported tag token.</param>
    /// <param name="state">Render state.</param>
    /// <param name="tokenIndex">Index of the tag token.</param>
    /// <returns><paramref name="tokenIndex"/>.</returns>
    private static int EmitUnsupported(TemplateUnit template, LayoutToken t, RenderState state, int tokenIndex)
    {
        var body = template.Bytes.AsSpan(t.PayloadStart, t.PayloadEnd - t.PayloadStart);
        LayoutsLoggingHelper.LogUnsupportedTag(state.Logger, Encoding.UTF8.GetString(body));
        Write(state.Writer, template.Bytes.AsSpan(t.Start, t.End - t.Start));
        return tokenIndex;
    }

    /// <summary>Renders a <c>{% block name %} … {% endblock %}</c> region.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="openIndex">Index of the <c>{% block %}</c> token.</param>
    /// <param name="state">Render state.</param>
    /// <param name="blocks">Block overlay.</param>
    /// <param name="depth">Current include depth.</param>
    /// <returns>Index of the matching <c>{% endblock %}</c>.</returns>
    private static int RenderBlock(TemplateUnit template, int openIndex, RenderState state, BlockOverlay? blocks, int depth)
    {
        var open = template.Tokens[openIndex];
        var blockName = template.Bytes[open.PayloadStart..open.PayloadEnd];
        var (innerStart, innerEnd, closeIndex) = FindBlockEnd(template.Tokens, openIndex);

        if (blocks is not null && blocks.TryGetChildRange(blockName, out var childRange))
        {
            blocks.RememberParent(blockName, template, new(innerStart, innerEnd));
            RenderRange(blocks.Child, childRange, state, blocks, blockName, depth);
            return closeIndex;
        }

        RenderRange(template, new(innerStart, innerEnd), state, blocks, currentBlock: null, depth);
        return closeIndex;
    }

    /// <summary>Walks token-by-token from <paramref name="openIndex"/> + 1, pairing nested <c>{% block %}</c>s with their <c>{% endblock %}</c>s.</summary>
    /// <param name="tokens">Token stream.</param>
    /// <param name="openIndex">Index of the outer block-open token.</param>
    /// <returns>Inner-token range and the index of the matching close.</returns>
    private static (int InnerStart, int InnerEnd, int CloseIndex) FindBlockEnd(List<LayoutToken> tokens, int openIndex)
    {
        var depth = 1;
        for (var j = openIndex + 1; j < tokens.Count; j++)
        {
            var t = tokens[j];
            if (t.Kind is LayoutTokenKind.BlockOpen)
            {
                depth++;
            }
            else if (t.Kind is LayoutTokenKind.BlockClose)
            {
                depth--;
                if (depth is 0)
                {
                    return (openIndex + 1, j, j);
                }
            }
        }

        // Unterminated block — render to end.
        return (openIndex + 1, tokens.Count, tokens.Count - 1);
    }

    /// <summary>Locates a token's index within its template.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Token.</param>
    /// <returns>Zero-based index of <paramref name="t"/>; -1 when absent.</returns>
    private static int IndexOf(TemplateUnit template, LayoutToken t) => template.Tokens.IndexOf(t);

    /// <summary>True when every byte of <paramref name="span"/> is ASCII whitespace.</summary>
    /// <param name="span">UTF-8 bytes.</param>
    /// <returns>True for whitespace-only.</returns>
    private static bool IsAllWhitespace(ReadOnlySpan<byte> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (!AsciiByteHelpers.IsAsciiWhitespace(span[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to copy.</param>
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

    /// <summary>Inclusive-exclusive token-index range covering a block body.</summary>
    /// <param name="Start">Inclusive start.</param>
    /// <param name="End">Exclusive end.</param>
    private readonly record struct BlockRange(int Start, int End);

    /// <summary>Bundle of resources every render-time call site needs.</summary>
    /// <param name="TemplateDirectory">Template root.</param>
    /// <param name="Context">Variable bag.</param>
    /// <param name="MaxDepth">Recursion cap.</param>
    /// <param name="Writer">UTF-8 sink.</param>
    /// <param name="Logger">Logger.</param>
    /// <param name="Cache">Optional per-build template cache; <see langword="null"/> disables caching for this render.</param>
    private readonly record struct RenderState(DirectoryPath TemplateDirectory, LayoutContext Context, int MaxDepth, IBufferWriter<byte> Writer, ILogger Logger, TemplateCache? Cache);

    /// <summary>Parent template + body range stashed for a <c>{{ super() }}</c> reference.</summary>
    /// <param name="Template">Parent template unit.</param>
    /// <param name="Range">Inclusive-exclusive body range.</param>
    private readonly record struct ParentEntry(TemplateUnit Template, BlockRange Range);

    /// <summary>Tracks the child-block override map plus the parent body of the block currently being rendered (so <c>{{ super() }}</c> can splice it).</summary>
    private sealed class BlockOverlay
    {
        /// <summary>Initializes a new instance of the <see cref="BlockOverlay"/> class.</summary>
        /// <param name="childBlocks">Child block-name → token range.</param>
        /// <param name="child">Child template unit.</param>
        public BlockOverlay(Dictionary<byte[], BlockRange> childBlocks, TemplateUnit child)
        {
            ArgumentNullException.ThrowIfNull(childBlocks);
            Children = childBlocks;
            Child = child;
            Parents = new(ByteArrayComparer.Instance);
        }

        /// <summary>Gets the child override map.</summary>
        public Dictionary<byte[], BlockRange> Children { get; }

        /// <summary>Gets the child template unit.</summary>
        public TemplateUnit Child { get; }

        /// <summary>Gets the parent block-name → (parent template, body range) map.</summary>
        public Dictionary<byte[], ParentEntry> Parents { get; }

        /// <summary>True when <paramref name="name"/> is overridden in the child.</summary>
        /// <param name="name">UTF-8 block name.</param>
        /// <param name="range">Child block range on hit.</param>
        /// <returns>True on hit.</returns>
        public bool TryGetChildRange(ReadOnlySpan<byte> name, out BlockRange range) =>
            Children.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(name, out range);

        /// <summary>Records the parent body for <paramref name="name"/> so a subsequent <c>{{ super() }}</c> can splice it.</summary>
        /// <param name="name">UTF-8 block name.</param>
        /// <param name="parent">Parent template unit.</param>
        /// <param name="range">Inclusive-exclusive parent body range.</param>
        public void RememberParent(byte[] name, TemplateUnit parent, BlockRange range) =>
            Parents[name] = new(parent, range);

        /// <summary>Pulls the parent body range previously stashed by <see cref="RememberParent"/>.</summary>
        /// <param name="name">UTF-8 block name.</param>
        /// <param name="entry">Parent entry on hit.</param>
        /// <returns>True when a parent body was recorded.</returns>
        public bool TryGetParentRange(byte[] name, out ParentEntry entry) =>
            Parents.TryGetValue(name, out entry);
    }
}
