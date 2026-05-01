// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Optional companion contract for plugins that want to rewrite the
/// raw Markdown source before <see cref="NuStreamDocs.MarkdownRenderer"/> runs.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by plugins that translate a custom block syntax into
/// HTML the renderer can pass through verbatim — admonitions
/// (<c>!!! note</c>), Material tabs (<c>=== "Tab"</c>), collapsible
/// details (<c>??? note</c>), footnotes, definition lists, and so on.
/// The build pipeline calls every registered preprocessor in
/// registration order, threading the output of one into the input of
/// the next.
/// </para>
/// <para>
/// The contract is byte-in / byte-out so the UTF-8 pipeline stays
/// allocation-light. A no-op implementation should simply copy the
/// input bytes into the output writer.
/// </para>
/// <para>
/// Path-aware preprocessors (frontmatter inheritance, per-page
/// metadata injection) override <c>Preprocess</c>
/// instead of the path-blind overload below. The default
/// implementation forwards to the path-blind overload so existing
/// preprocessors compile unchanged.
/// </para>
/// </remarks>
public interface IMarkdownPreprocessor
{
    /// <summary>Returns true when <paramref name="source"/> may contain markers this preprocessor recognises.</summary>
    /// <param name="source">UTF-8 markdown bytes about to be passed to <see cref="Preprocess(ReadOnlySpan{byte}, IBufferWriter{byte})"/>.</param>
    /// <returns>
    /// True when the preprocessor must run (the default — preserves back-compat). Implementations
    /// override this with a vectorised <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/>
    /// (or <c>SearchValues</c>) probe for their distinctive marker bytes; the pipeline then skips
    /// the rewriter entirely when no marker is anywhere in the source. On the rxui corpus, where
    /// most pages use only a subset of the registered preprocessors, this cuts both the per-page
    /// CPU work and the writer-buffer growth that the line-walk would otherwise drive.
    /// </returns>
    bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/> with the plugin's substitutions applied.</summary>
    /// <param name="source">UTF-8 markdown bytes (as read from disk for the first preprocessor; the previous preprocessor's output for subsequent ones).</param>
    /// <param name="writer">UTF-8 sink the rewritten markdown is written into.</param>
    void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer);

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/> with knowledge of <paramref name="relativePath"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="relativePath">
    /// Page path relative to the input root (e.g. <c>guide/intro.md</c>);
    /// preprocessors that key behaviour on the page identity
    /// (e.g. metadata injection) override this overload.
    /// </param>
    void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath) =>
        Preprocess(source, writer);
}
