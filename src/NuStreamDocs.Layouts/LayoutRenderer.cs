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
    /// <summary>Prefix used when constructing missing-variable diagnostics.</summary>
    private const string PagePrefix = "page.";

    /// <summary>The <c>.html</c> suffix appended when a template name was supplied without one.</summary>
    private const string HtmlExtension = ".html";

    /// <summary>Renders <paramref name="templateName"/> against <paramref name="context"/> and writes the result to <paramref name="writer"/>.</summary>
    /// <param name="templateName">UTF-8 layout file name (e.g. <c>"page.html"u8</c>).</param>
    /// <param name="templateDirectory">Directory layouts are loaded from.</param>
    /// <param name="context">Variable bag.</param>
    /// <param name="maxDepth">Recursion cap on <c>{% include %}</c> / <c>{% extends %}</c> expansion.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="logger">Logger for warnings.</param>
    /// <param name="cache">Optional per-build template cache; when <see langword="null"/> every load re-reads from disk.</param>
    /// <returns>True when the template was loaded and rendered; false when the template file was missing.</returns>
    public static bool Render(
        ReadOnlySpan<byte> templateName,
        in DirectoryPath templateDirectory,
        LayoutContext context,
        int maxDepth,
        IBufferWriter<byte> writer,
        ILogger logger,
        TemplateCache? cache)
    {
        if (!TryLoadTemplate(templateName, templateDirectory, cache, out var template, out var resolvedPath))
        {
            LayoutsLoggingHelper.LogMissingTemplate(logger, resolvedPath.Value ?? string.Empty);
            return false;
        }

        RenderState state = new(templateDirectory, context, maxDepth, writer, logger, cache);

        if (TryFindExtends(template, out var parentTarget))
        {
            RenderWithInheritance(parentTarget, template, state);
            return true;
        }

        RenderTokens(template, state, null, null, 0);
        return true;
    }

    /// <summary>Loads a template, trying both the literal name and a <c>.html</c>-appended variant.</summary>
    /// <param name="templateName">UTF-8 layout file name.</param>
    /// <param name="templateDirectory">Resolution root.</param>
    /// <param name="cache">Optional per-build cache.</param>
    /// <param name="template">Parsed template on success.</param>
    /// <param name="resolvedPath">Last attempted absolute path (for diagnostics).</param>
    /// <returns>True when the file was loaded.</returns>
    private static bool TryLoadTemplate(
        ReadOnlySpan<byte> templateName,
        in DirectoryPath templateDirectory,
        TemplateCache? cache,
        out TemplateUnit template,
        out FilePath resolvedPath)
    {
        template = default;
        resolvedPath = default;
        if (templateName.IsEmpty || templateDirectory.IsEmpty)
        {
            return false;
        }

        if (cache is not null && cache.TryGet(templateName, out var hit))
        {
            template = hit.Unit;
            resolvedPath = hit.ResolvedPath;
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
    private static bool TryLoadFromDisk(
        ReadOnlySpan<byte> templateName,
        in DirectoryPath templateDirectory,
        TemplateCache? cache,
        out TemplateUnit template,
        out FilePath resolvedPath)
    {
        template = default;
        var nameString = Encoding.UTF8.GetString(templateName);
        var path = Path.GetFullPath(nameString, templateDirectory.Value);
        resolvedPath = new(path);
        if (File.Exists(path))
        {
            template = TemplateUnit.From(File.ReadAllBytes(path));
            cache?.Add(templateName.ToArray(), new(template, resolvedPath));
            return true;
        }

        if (nameString.AsSpan().EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Explicit single-allocation compose via the project's StringCompose helper —
        // sidesteps interpolation / Concat compiler-lowering uncertainty.
        var altPath = Path.GetFullPath(StringCompose.Concat(nameString, HtmlExtension), templateDirectory.Value);
        resolvedPath = new(altPath);
        if (!File.Exists(altPath))
        {
            return false;
        }

        template = TemplateUnit.From(File.ReadAllBytes(altPath));
        cache?.Add(templateName.ToArray(), new(template, resolvedPath));
        return true;
    }

    /// <summary>Searches <paramref name="template"/> for a leading <see cref="LayoutTokenKind.Extends"/>.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="parentTarget">Resolved parent template name on success.</param>
    /// <returns>True when the first non-literal-only-whitespace token is an extends.</returns>
    private static bool TryFindExtends(in TemplateUnit template, out byte[] parentTarget)
    {
        parentTarget = [];
        var tokens = template.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            switch (t.Kind)
            {
                case LayoutTokenKind.Literal
                    when !AsciiByteHelpers.IsAllAsciiWhitespace(template.Bytes.AsSpan(t.Start, t.End - t.Start)):
                    return false;
                case LayoutTokenKind.Literal:
                    continue;
                case LayoutTokenKind.Extends:
                    {
                        parentTarget = template.Bytes[t.PayloadStart..t.PayloadEnd];
                        return true;
                    }

                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Renders the parent template with the child's blocks overriding the parent's same-named blocks.</summary>
    /// <param name="parentTarget">UTF-8 parent template name.</param>
    /// <param name="child">Child template.</param>
    /// <param name="state">Render state.</param>
    private static void RenderWithInheritance(byte[] parentTarget, in TemplateUnit child, in RenderState state)
    {
        if (!TryLoadTemplate(parentTarget, state.TemplateDirectory, state.Cache, out var parent, out var parentPath))
        {
            LayoutsLoggingHelper.LogMissingTemplate(state.Logger, parentPath);

            // Fall back: render the child directly (its blocks become the output).
            RenderTokens(child, state, null, null, 0);
            return;
        }

        Dictionary<byte[], BlockRange> childBlocks = new(ByteArrayComparer.Instance);
        CollectBlocks(child, childBlocks);

        BlockOverlay overlay = new(childBlocks, child);
        RenderTokens(parent, state, overlay, null, 0);
    }

    /// <summary>Walks <paramref name="template"/> and records the token-index range of each top-level <c>{% block name %}</c>.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="blocks">Sink dictionary, name → block range.</param>
    private static void CollectBlocks(in TemplateUnit template, Dictionary<byte[], BlockRange> blocks)
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
    private static void RenderTokens(
        in TemplateUnit template,
        in RenderState state,
        BlockOverlay? blocks,
        byte[]? currentBlock,
        int depth) =>
        RenderRange(template, new(0, template.Tokens.Count), state, blocks, currentBlock, depth);

    /// <summary>Renders a token-index range from <paramref name="template"/>.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="range">Inclusive-exclusive token range.</param>
    /// <param name="state">Render state.</param>
    /// <param name="blocks">Block overlay.</param>
    /// <param name="currentBlock">Current block name.</param>
    /// <param name="depth">Current include depth.</param>
    private static void RenderRange(
        in TemplateUnit template,
        in BlockRange range,
        in RenderState state,
        BlockOverlay? blocks,
        byte[]? currentBlock,
        int depth)
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
    private static int RenderOne(
        in TemplateUnit template,
        int i,
        in RenderState state,
        BlockOverlay? blocks,
        byte[]? currentBlock,
        int depth)
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
            _ => i
        };
    }

    /// <summary>Copies a literal token's bytes to the writer.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Token.</param>
    /// <param name="state">Render state.</param>
    /// <returns>The token's own index; the caller advances past it.</returns>
    private static int EmitLiteral(in TemplateUnit template, in LayoutToken t, in RenderState state)
    {
        Write(state.Writer, template.Bytes.AsSpan(t.Start, t.End - t.Start));
        return IndexOf(template, t);
    }

    /// <summary>Emits a variable's value (or empty + warning when missing).</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Variable token.</param>
    /// <param name="state">Render state.</param>
    /// <returns>The token's own index.</returns>
    private static int EmitVariable(in TemplateUnit template, in LayoutToken t, in RenderState state)
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
    private static int EmitSuper(
        byte[]? currentBlock,
        BlockOverlay? blocks,
        in RenderState state,
        int depth,
        int tokenIndex)
    {
        if (currentBlock is null || blocks is null || !blocks.TryGetParentRange(currentBlock, out var entry))
        {
            LayoutsLoggingHelper.LogSuperOutsideBlock(state.Logger);
            return tokenIndex;
        }

        RenderRange(entry.Template, entry.Range, state, blocks, null, depth);
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
    private static int EmitInclude(
        in TemplateUnit template,
        in LayoutToken t,
        in RenderState state,
        BlockOverlay? blocks,
        int depth,
        int tokenIndex)
    {
        var includeName = template.Bytes.AsSpan(t.PayloadStart, t.PayloadEnd - t.PayloadStart);
        if (depth >= state.MaxDepth)
        {
            LayoutsLoggingHelper.LogIncludeDepthExceeded(
                state.Logger,
                state.MaxDepth,
                Encoding.UTF8.GetString(includeName));
            return tokenIndex;
        }

        if (!TryLoadTemplate(includeName, state.TemplateDirectory, state.Cache, out var included, out var includePath))
        {
            LayoutsLoggingHelper.LogMissingInclude(state.Logger, includePath);
            return tokenIndex;
        }

        RenderTokens(included, state, blocks, null, depth + 1);
        return tokenIndex;
    }

    /// <summary>Logs an unsupported tag and copies its source bytes through.</summary>
    /// <param name="template">Template unit.</param>
    /// <param name="t">Unsupported tag token.</param>
    /// <param name="state">Render state.</param>
    /// <param name="tokenIndex">Index of the tag token.</param>
    /// <returns><paramref name="tokenIndex"/>.</returns>
    private static int EmitUnsupported(in TemplateUnit template, in LayoutToken t, in RenderState state, int tokenIndex)
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
    private static int RenderBlock(
        in TemplateUnit template,
        int openIndex,
        in RenderState state,
        BlockOverlay? blocks,
        int depth)
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

        RenderRange(template, new(innerStart, innerEnd), state, blocks, null, depth);
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
            switch (t.Kind)
            {
                case LayoutTokenKind.BlockOpen:
                    {
                        depth++;
                        break;
                    }

                case LayoutTokenKind.BlockClose:
                    {
                        depth--;
                        if (depth is 0)
                        {
                            return (openIndex + 1, j, j);
                        }

                        break;
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
    private static int IndexOf(in TemplateUnit template, in LayoutToken t) => template.Tokens.IndexOf(t);

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
    /// <param name="Cache">Optional per-build template cache.</param>
    private readonly record struct RenderState(
        DirectoryPath TemplateDirectory,
        LayoutContext Context,
        int MaxDepth,
        IBufferWriter<byte> Writer,
        ILogger Logger,
        TemplateCache? Cache);

    /// <summary>Parent template + body range stashed for a <c>{{ super() }}</c> reference.</summary>
    /// <param name="Template">Parent template unit.</param>
    /// <param name="Range">Inclusive-exclusive body range.</param>
    private readonly record struct ParentEntry(TemplateUnit Template, BlockRange Range);

    /// <summary>Tracks child block overrides and the parent body currently being rendered for <c>{{ super() }}</c>.</summary>
    private sealed class BlockOverlay
    {
        /// <summary>Initializes a new instance of the <see cref="BlockOverlay"/> class.</summary>
        /// <param name="childBlocks">Child block-name → token range.</param>
        /// <param name="child">Child template unit.</param>
        public BlockOverlay(Dictionary<byte[], BlockRange> childBlocks, in TemplateUnit child)
        {
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
        public void RememberParent(byte[] name, in TemplateUnit parent, in BlockRange range) =>
            Parents[name] = new(parent, range);

        /// <summary>Pulls the parent body range previously stashed by <see cref="RememberParent"/>.</summary>
        /// <param name="name">UTF-8 block name.</param>
        /// <param name="entry">Parent entry on hit.</param>
        /// <returns>True when a parent body was recorded.</returns>
        public bool TryGetParentRange(byte[] name, out ParentEntry entry) =>
            Parents.TryGetValue(name, out entry);
    }
}
