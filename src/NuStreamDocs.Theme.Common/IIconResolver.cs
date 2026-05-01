// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Optional plug-in that the icon-shortcode rewriter consults before
/// falling back to the default font-ligature span. When implementations
/// claim a name, they write the inline-SVG (or whatever icon markup
/// they produce) directly to the supplied <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <remarks>
/// Writing through <see cref="IBufferWriter{T}"/> rather than returning
/// a <see cref="ReadOnlySpan{T}"/> lets resolvers stream the icon as a
/// pre-allocated wrapper plus per-icon body without ever assembling a
/// full SVG span — the MDI bundle, for instance, ships only the
/// <c>&lt;path d="…"/&gt;</c> data and re-emits the surrounding
/// <c>&lt;svg&gt;</c> wrapper as <c>"…"u8</c> literals stamped once per
/// hit. Non-MDI implementations that already hold full SVG bytes can
/// just call <see cref="IBufferWriter{T}"/>.<see cref="IBufferWriter{T}.GetSpan"/>
/// + <see cref="IBufferWriter{T}.Advance"/> with their existing
/// payload in a single write.
/// </remarks>
public interface IIconResolver
{
    /// <summary>Tries to resolve <paramref name="iconName"/> and write its inline markup to <paramref name="writer"/>.</summary>
    /// <param name="iconName">UTF-8 icon name bytes; matches what appears between the trailing <c>-</c> of the family prefix and the closing <c>:</c>.</param>
    /// <param name="writer">UTF-8 sink to receive the inline icon markup on hit.</param>
    /// <returns>True when the resolver claimed this icon name and wrote markup to <paramref name="writer"/>.</returns>
    bool TryResolve(ReadOnlySpan<byte> iconName, IBufferWriter<byte> writer);
}
