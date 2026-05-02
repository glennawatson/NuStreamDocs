// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin contract that contributes a fence handler to the
/// pymdownx-style superfences dispatcher. Any
/// <see cref="IDocPlugin"/> can additionally implement this to
/// claim a fenced-code language and render it as bespoke HTML
/// instead of the default <c>&lt;pre&gt;&lt;code&gt;</c> block.
/// </summary>
/// <remarks>
/// The dispatcher (in <c>NuStreamDocs.SuperFences</c>) collects
/// every handler exposed by registered plugins during
/// <c>OnConfigure</c>, then in <c>OnRenderPage</c> walks the
/// rendered HTML looking for the <c>&lt;pre&gt;&lt;code class="language-{Language}"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks whose language matches a registered handler. Matched
/// blocks are replaced wholesale with the handler's output.
/// </remarks>
public interface ICustomFenceHandler
{
    /// <summary>Gets the UTF-8 language identifier this handler claims (e.g. <c>"mermaid"u8</c>, <c>"math"u8</c>).</summary>
    /// <remarks>Implementations should return a UTF-8 literal so the dispatcher's per-page byte-keyed lookup never has to transcode at registration or dispatch time.</remarks>
    ReadOnlySpan<byte> Language { get; }

    /// <summary>Renders <paramref name="content"/> into <paramref name="writer"/>.</summary>
    /// <param name="content">UTF-8 fence body, with HTML entities decoded back to their literal bytes.</param>
    /// <param name="writer">UTF-8 sink that replaces the source <c>&lt;pre&gt;&lt;code&gt;</c> block.</param>
    void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer);
}
