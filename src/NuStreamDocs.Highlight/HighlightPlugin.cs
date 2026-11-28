// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Html;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Replaces <c>&lt;pre&gt;&lt;code class="language-X"&gt;…&lt;/code&gt;&lt;/pre&gt;</c> bodies
/// with classed token spans produced by a <see cref="Lexer"/>. When
/// <see cref="HighlightOptions.AutoDetectLanguage"/> is on, unlabeled blocks are
/// also scored and labelled when a high-confidence match is found.
/// </summary>
[System.Diagnostics.DebuggerDisplay("HighlightPlugin: {Name}")]
public sealed class HighlightPlugin : IPagePostRenderPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>The <c>&lt;link rel="stylesheet"&gt;</c> snippet the page-shell template injects into <c>&lt;head&gt;</c>.</summary>
    private static readonly byte[] HeadExtraSnippet =
        [.. "<link rel=\"stylesheet\" href=\""u8, .. "/assets/stylesheets/highlight.css"u8, .. "\">\n"u8];

    /// <summary>The lexer registry built once at configure time.</summary>
    private readonly LexerRegistry _registry;

    /// <summary>Configured options.</summary>
    private readonly HighlightOptions _options;

    /// <summary>Initializes a new instance of the <see cref="HighlightPlugin"/> class with default options.</summary>
    public HighlightPlugin()
        : this(HighlightOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HighlightPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public HighlightPlugin(HighlightOptions options)
    {
        _registry = LexerRegistry.Build(options.ExtraLexers);
        _options = options;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "highlight"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets =>
        [(HighlightStylesheet.AssetPath, HighlightStylesheet.GetBytes())];

    /// <summary>Gets the opening tag for a labeled code block.</summary>
    private static ReadOnlySpan<byte> PreOpen => "<pre><code class=\"language-"u8;

    /// <summary>Gets the opening tag for an unlabeled code block.</summary>
    private static ReadOnlySpan<byte> PreOpenUnlabeled => "<pre><code>"u8;

    /// <summary>Gets the closing tag that terminates a highlighted body.</summary>
    private static ReadOnlySpan<byte> CodeClose => "</code>"u8;

    /// <summary>Gets the closing tag of the surrounding preformatted block.</summary>
    private static ReadOnlySpan<byte> PreClose => "</pre>"u8;

    /// <summary>Gets the opening tag of a plain ordered list that may serve as a code callout legend.</summary>
    private static ReadOnlySpan<byte> PlainOrderedListOpen => "<ol>"u8;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html)
    {
        if (html.IndexOf(PreOpen) >= 0)
        {
            return true;
        }

        return _options.AutoDetectLanguage && html.IndexOf(PreOpenUnlabeled) >= 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PostRender(in PagePostRenderContext context) => Highlight(context.Html, context.Output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteHeadExtra(IBufferWriter<byte> writer) => writer.Write(HeadExtraSnippet);

    /// <summary>Decodes HTML-escaped UTF-8 bytes into a fresh byte array; copies verbatim when no entities are present.</summary>
    /// <param name="bytes">Source bytes.</param>
    /// <returns>Decoded bytes.</returns>
    private static byte[] HtmlUnescapeBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IndexOf((byte)'&') < 0)
        {
            return bytes.ToArray();
        }

        using var rental = PageBuilderPool.Rent(bytes.Length);
        var sink = rental.Writer;
        HtmlEntityDecoder.DecodeInto(sink, bytes);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Slices the <c>data-info</c> attribute value out of the <c>&lt;code …&gt;</c> opening, or returns empty when absent.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="afterLang">Offset just past the closing quote of <c>language-X</c>.</param>
    /// <param name="openTagEnd">Offset of the closing <c>&gt;</c> of the open tag.</param>
    /// <returns>Decoded fence-info bytes (HTML entities unescaped).</returns>
    private static byte[] ExtractDataInfo(ReadOnlySpan<byte> source, int afterLang, int openTagEnd)
    {
        var attrs = source[afterLang..openTagEnd];
        var marker = " data-info=\""u8;
        var idx = attrs.IndexOf(marker);
        if (idx < 0)
        {
            return [];
        }

        var valStart = idx + marker.Length;
        var endRel = attrs[valStart..].IndexOf((byte)'"');
        return endRel < 0 ? [] : HtmlUnescapeBytes(attrs.Slice(valStart, endRel));
    }

    /// <summary>Locates the closing <c>&lt;/code&gt;</c> and <c>&lt;/pre&gt;</c> tags starting from <paramref name="bodyStart"/>.</summary>
    /// <param name="source">Full HTML bytes.</param>
    /// <param name="bodyStart">Start offset of the code body.</param>
    /// <param name="bodyEnd">Resolved start offset of <c>&lt;/code&gt;</c>.</param>
    /// <param name="afterBlock">Resolved offset immediately past <c>&lt;/pre&gt;</c>.</param>
    /// <returns><c>true</c> when both closing tags were found.</returns>
    private static bool TryLocateClosingTags(
        ReadOnlySpan<byte> source,
        int bodyStart,
        out int bodyEnd,
        out int afterBlock)
    {
        var bodyEndRel = source[bodyStart..].IndexOf(CodeClose);
        if (bodyEndRel < 0)
        {
            bodyEnd = 0;
            afterBlock = 0;
            return false;
        }

        bodyEnd = bodyStart + bodyEndRel;
        var afterCodeClose = bodyEnd + CodeClose.Length;
        var preCloseRel = source[afterCodeClose..].IndexOf(PreClose);
        if (preCloseRel < 0)
        {
            afterBlock = 0;
            return false;
        }

        afterBlock = afterCodeClose + preCloseRel + PreClose.Length;
        return true;
    }

    /// <summary>Rewrites an immediately following <c>&lt;ol&gt;</c> into <c>&lt;ol class="nd-code-annotations"&gt;</c> when the block emitted callouts.</summary>
    /// <param name="source">Full page HTML bytes.</param>
    /// <param name="afterBlock">Offset right after <c>&lt;/pre&gt;</c>.</param>
    /// <param name="writer">Destination sink.</param>
    /// <returns>New resume offset.</returns>
    private static int TryRewriteTrailingCalloutList(ReadOnlySpan<byte> source, int afterBlock, IBufferWriter<byte> writer)
    {
        var whitespaceEnd = afterBlock;
        while (whitespaceEnd < source.Length && source[whitespaceEnd] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            whitespaceEnd++;
        }

        if (!source[whitespaceEnd..].StartsWith(PlainOrderedListOpen))
        {
            return afterBlock;
        }

        Write(writer, source[afterBlock..whitespaceEnd]);
        Write(writer, "<ol class=\"nd-code-annotations\">"u8);
        return whitespaceEnd + PlainOrderedListOpen.Length;
    }

    /// <summary>Reads <c>linenums</c>, <c>hl_lines</c>, and <c>{...}</c> fence-info attributes.</summary>
    /// <param name="dataInfo">Decoded fence-info bytes.</param>
    /// <param name="startLine">Updated starting line number when <c>linenums</c> is present.</param>
    /// <param name="highlighted">Populated highlighted line set when range tokens are present.</param>
    private static void ReadFenceLineAttributes(
        ReadOnlySpan<byte> dataInfo,
        ref int? startLine,
        out HashSet<int>? highlighted)
    {
        highlighted = null;
        if (dataInfo.IsEmpty)
        {
            return;
        }

        if (FenceAttrParser.TryGetLineNums(dataInfo, out var lineNumVal))
        {
            startLine = LineHighlightSet.ParseStartLine(lineNumVal);
        }

        if (FenceAttrParser.TryGetHighlightLines(dataInfo, out var hlVal))
        {
            LineHighlightSet.ParseInto(hlVal, ref highlighted);
        }

        if (FenceAttrParser.TryGetBraceHighlightLines(dataInfo, out var braceVal))
        {
            LineHighlightSet.ParseInto(braceVal, ref highlighted);
        }
    }

    /// <summary>Slices off a single trailing <c>\n</c> when present.</summary>
    /// <param name="bytes">Source byte array.</param>
    /// <param name="trailingNewline"><c>true</c> when <paramref name="bytes"/> ended with <c>\n</c>.</param>
    /// <returns>Body memory without the trailing <c>\n</c>.</returns>
    private static ReadOnlyMemory<byte> StripTrailingNewline(byte[] bytes, out bool trailingNewline)
    {
        trailingNewline = bytes.Length > 0 && bytes[^1] == (byte)'\n';
        return trailingNewline ? bytes.AsMemory(0, bytes.Length - 1) : bytes;
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

    /// <summary>Walks <paramref name="source"/>, substituting highlighted bodies for every recognized code block.</summary>
    /// <param name="source">Snapshot of the rendered HTML.</param>
    /// <param name="writer">UTF-8 sink (the original page buffer).</param>
    private void Highlight(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var cursor = 0;
        while (cursor < source.Length)
        {
            var rest = source[cursor..];
            var labeledRel = rest.IndexOf(PreOpen);
            var unlabeledRel = _options.AutoDetectLanguage ? rest.IndexOf(PreOpenUnlabeled) : -1;

            if (labeledRel < 0 && unlabeledRel < 0)
            {
                Write(writer, rest);
                return;
            }

            var labeledFirst = labeledRel >= 0 && (unlabeledRel < 0 || labeledRel <= unlabeledRel);
            if (labeledFirst)
            {
                var preStart = cursor + labeledRel;
                Write(writer, source[cursor..preStart]);
                cursor = ProcessBlock(source, preStart, writer);
                continue;
            }

            var unlabeledStart = cursor + unlabeledRel;
            Write(writer, source[cursor..unlabeledStart]);
            cursor = ProcessUnlabeledBlock(source, unlabeledStart, writer);
        }
    }

    /// <summary>Processes one unlabeled block: runs the detector against the body and either emits a highlighted (labeled) form or copies the original through unchanged.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="preStart">Offset of <c>&lt;pre&gt;</c>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Offset just past the closing <c>&lt;/pre&gt;</c> on success, otherwise the source length (caller's loop terminates).</returns>
    private int ProcessUnlabeledBlock(ReadOnlySpan<byte> source, int preStart, IBufferWriter<byte> writer)
    {
        var bodyStart = preStart + PreOpenUnlabeled.Length;
        if (!TryLocateClosingTags(source, bodyStart, out var bodyEnd, out var afterBlock))
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        var body = source[bodyStart..bodyEnd];
        if (!LanguageDetector.TryDetect(body, _registry, _options.DetectionLanguages, out var detectedLanguage))
        {
            Write(writer, source[preStart..afterBlock]);
            return afterBlock;
        }

        EmitDetectedBlock(detectedLanguage, body, writer);
        return afterBlock;
    }

    /// <summary>Emits the rewritten form of an auto-detected block: opening wrapper + <c>&lt;pre&gt;&lt;code class="language-…"&gt;</c> + highlighted body + close + closing wrapper.</summary>
    /// <param name="languageBytes">Detected language alias bytes.</param>
    /// <param name="body">Original (HTML-escaped) body bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitDetectedBlock(
        ReadOnlySpan<byte> languageBytes,
        ReadOnlySpan<byte> body,
        IBufferWriter<byte> writer)
    {
        var plan = PrepareBlock(default, body);
        EmitOpeningWrapper(default, plan.HasFocus, writer);
        Write(writer, "<pre><code class=\"language-"u8);
        Write(writer, languageBytes);
        Write(writer, "\">"u8);
        EmitBody(writer, languageBytes, body, plan);
        Write(writer, CodeClose);
        Write(writer, PreClose);
        EmitClosingWrapper(writer);
    }

    /// <summary>Processes one matched <c>&lt;pre&gt;&lt;code class="language-…"&gt;</c> block; emits the rewritten form and returns the offset to resume scanning at.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="preStart">Offset of <c>&lt;pre&gt;</c>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Offset just past the closing <c>&lt;/pre&gt;</c> when matched, otherwise the source length.</returns>
    private int ProcessBlock(ReadOnlySpan<byte> source, int preStart, IBufferWriter<byte> writer)
    {
        var langStart = preStart + PreOpen.Length;
        var langEndRel = source[langStart..].IndexOf((byte)'"');
        if (langEndRel <= 0)
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        var languageBytes = source.Slice(langStart, langEndRel);
        var afterLang = langStart + langEndRel + 1;
        var openTagEndRel = source[afterLang..].IndexOf((byte)'>');
        if (openTagEndRel < 0)
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        var openTagEnd = afterLang + openTagEndRel;
        var dataInfo = ExtractDataInfo(source, afterLang, openTagEnd);
        var bodyStart = openTagEnd + 1;
        if (!TryLocateClosingTags(source, bodyStart, out var bodyEnd, out var afterBlock))
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        var hasCallouts = EmitWrappedBlock(source, preStart, bodyStart, bodyEnd, dataInfo, languageBytes, writer);
        return hasCallouts ? TryRewriteTrailingCalloutList(source, afterBlock, writer) : afterBlock;
    }

    /// <summary>Emits the rewritten block and returns <c>true</c> when inline callout badges were emitted.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="preStart">Offset of <c>&lt;pre&gt;</c>.</param>
    /// <param name="bodyStart">Offset of the first body byte.</param>
    /// <param name="bodyEnd">Offset of <c>&lt;/code&gt;</c>.</param>
    /// <param name="dataInfo">Decoded fence-info bytes (may be empty).</param>
    /// <param name="languageBytes">Language alias bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns><c>true</c> when at least one numbered callout badge was emitted.</returns>
    private bool EmitWrappedBlock(
        ReadOnlySpan<byte> source,
        int preStart,
        int bodyStart,
        int bodyEnd,
        ReadOnlySpan<byte> dataInfo,
        ReadOnlySpan<byte> languageBytes,
        IBufferWriter<byte> writer)
    {
        var body = source[bodyStart..bodyEnd];
        var plan = PrepareBlock(dataInfo, body);
        EmitOpeningWrapper(dataInfo, plan.HasFocus, writer);

        Write(writer, source[preStart..bodyStart]);
        EmitBody(writer, languageBytes, body, plan);
        Write(writer, CodeClose);
        Write(writer, PreClose);

        EmitClosingWrapper(writer);
        return plan.HasCallouts;
    }

    /// <summary>Emits the opening wrapper div + title + copy button when <see cref="HighlightOptions.WrapInHighlightDiv"/> is on.</summary>
    /// <param name="dataInfo">Fence-info bytes.</param>
    /// <param name="hasFocus">Whether the block contains <c>[!code focus]</c> lines.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitOpeningWrapper(ReadOnlySpan<byte> dataInfo, bool hasFocus, IBufferWriter<byte> writer)
    {
        if (!_options.WrapInHighlightDiv)
        {
            return;
        }

        Write(writer, hasFocus ? "<div class=\"highlight nd-has-focus\">"u8 : "<div class=\"highlight\">"u8);
        EmitTitleBar(dataInfo, writer);
        EmitCopyButton(writer);
    }

    /// <summary>Emits the closing wrapper <c>&lt;/div&gt;</c> when <see cref="HighlightOptions.WrapInHighlightDiv"/> is on.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitClosingWrapper(IBufferWriter<byte> writer)
    {
        if (!_options.WrapInHighlightDiv)
        {
            return;
        }

        Write(writer, "</div>"u8);
    }

    /// <summary>Emits <c>&lt;span class="filename"&gt;{title}&lt;/span&gt;</c> when <see cref="HighlightOptions.EmitTitleBar"/> is on and a title attribute is present.</summary>
    /// <param name="dataInfo">Fence-info bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitTitleBar(ReadOnlySpan<byte> dataInfo, IBufferWriter<byte> writer)
    {
        if (!_options.EmitTitleBar || dataInfo.IsEmpty)
        {
            return;
        }

        if (!FenceAttrParser.TryGetTitle(dataInfo, out var title) || title.IsEmpty)
        {
            return;
        }

        Write(writer, "<span class=\"filename\">"u8);
        HtmlEscape.EscapeText(title, writer);
        Write(writer, "</span>"u8);
    }

    /// <summary>Emits a copy button (theme JS handles the click) when <see cref="HighlightOptions.CopyButton"/> is on.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitCopyButton(IBufferWriter<byte> writer)
    {
        if (!_options.CopyButton)
        {
            return;
        }

        Write(
            writer,
            "<button class=\"md-clipboard md-icon\" type=\"button\" aria-label=\"Copy to clipboard\"></button>"u8);
    }

    /// <summary>Resolves fence line-number/highlight attributes and inline directives for <paramref name="body"/>.</summary>
    /// <param name="dataInfo">Decoded fence-info bytes.</param>
    /// <param name="body">HTML-escaped code body.</param>
    /// <returns>Prepared rendering plan for the block.</returns>
    private BlockAnnotationPlan PrepareBlock(ReadOnlySpan<byte> dataInfo, ReadOnlySpan<byte> body)
    {
        int? startLine = _options.LineNumbers ? 1 : null;
        ReadFenceLineAttributes(dataInfo, ref startLine, out var highlighted);

        byte[]? unescaped = null;
        if (_options.EnableAnnotations && CodeAnnotationPreprocessor.MayContainAnnotations(body))
        {
            unescaped = HtmlUnescapeBytes(body);
            var bodySlice = StripTrailingNewline(unescaped, out var hasTrailingNl);
            if (CodeAnnotationPreprocessor.TryExtract(bodySlice.Span, out var clean, out var annotations, out var hasFocus, out var hasCallouts))
            {
                return new(true, clean, hasTrailingNl, annotations, highlighted, startLine, hasFocus, hasCallouts);
            }
        }

        if (startLine.HasValue || highlighted is not null)
        {
            var rawUnescaped = unescaped ?? HtmlUnescapeBytes(body);
            var cleanBody = StripTrailingNewline(rawUnescaped, out var hasTrailingNl);
            return new(true, cleanBody, hasTrailingNl, [], highlighted, startLine, false, false);
        }

        return new(false, unescaped, false, [], null, null, false, false);
    }

    /// <summary>Highlights <paramref name="body"/> when a lexer is registered for <paramref name="languageBytes"/>; otherwise emits it verbatim (or line-wrapped when annotated).</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="languageBytes">Language alias bytes from the class attribute.</param>
    /// <param name="body">Body bytes between <c>&gt;</c> and <c>&lt;/code&gt;</c>.</param>
    /// <param name="plan">Prepared annotation plan.</param>
    private void EmitBody(
        IBufferWriter<byte> writer,
        ReadOnlySpan<byte> languageBytes,
        ReadOnlySpan<byte> body,
        in BlockAnnotationPlan plan)
    {
        var hasLexer = _registry.TryGet(languageBytes, out var lexer) && lexer is not null;
        if (plan.UseAnnotatedEmitter)
        {
            AnnotatedHighlightEmitter.Emit(
                hasLexer ? lexer : null,
                plan.CleanBody,
                plan.TrailingNewline,
                plan.Annotations,
                plan.HighlightedLines,
                plan.StartLine,
                writer);
            return;
        }

        if (!hasLexer)
        {
            Write(writer, body);
            return;
        }

        var unescaped = !plan.CleanBody.IsEmpty ? plan.CleanBody : HtmlUnescapeBytes(body);
        HighlightEmitter.Emit(lexer!, unescaped, writer);
    }

    /// <summary>Carries resolved per-block annotation state between preparation and emission.</summary>
    /// <param name="UseAnnotatedEmitter"><c>true</c> when line-wrapping emission is required.</param>
    /// <param name="CleanBody">Cached unescaped (and optionally marker-stripped) code bytes.</param>
    /// <param name="TrailingNewline">Whether the original code block ended with <c>\n</c>.</param>
    /// <param name="Annotations">Per-line inline directives and callouts.</param>
    /// <param name="HighlightedLines">1-based highlighted line numbers.</param>
    /// <param name="StartLine">Optional starting line number.</param>
    /// <param name="HasFocus">Whether any line uses <c>[!code focus]</c>.</param>
    /// <param name="HasCallouts">Whether any line uses <c>(N)!</c>.</param>
    private readonly record struct BlockAnnotationPlan(
        bool UseAnnotatedEmitter,
        ReadOnlyMemory<byte> CleanBody,
        bool TrailingNewline,
        LineAnnotation[] Annotations,
        HashSet<int>? HighlightedLines,
        int? StartLine,
        bool HasFocus,
        bool HasCallouts);
}
