// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Optional plug-in that the icon-shortcode rewriter consults before
/// falling back to the default font-ligature span. Returning a non-empty
/// SVG span here causes the rewriter to inline that markup directly,
/// matching the inline-SVG shape mkdocs-material ships for its
/// <c>:material-foo:</c> icons (Material Design Icons / MDI).
/// </summary>
/// <remarks>
/// All inputs and outputs are byte spans so the rewriter never has to
/// materialise an icon name as a <see cref="string"/> or re-encode the
/// SVG bytes on emit. Implementations should treat the supplied name
/// as a verbatim slice into the source markdown — case-sensitive,
/// kebab-case, no surrounding colons or family prefix (callers strip
/// those before invoking).
/// </remarks>
public interface IIconResolver
{
    /// <summary>Tries to resolve <paramref name="iconName"/> to an inline-SVG body.</summary>
    /// <param name="iconName">UTF-8 icon name bytes; matches what appears between the trailing <c>-</c> of the family prefix and the closing <c>:</c>.</param>
    /// <param name="svg">UTF-8 SVG bytes to inline on hit; <c>default</c> on miss.</param>
    /// <returns>True when the resolver claims this icon name.</returns>
    bool TryResolve(ReadOnlySpan<byte> iconName, out ReadOnlySpan<byte> svg);
}
