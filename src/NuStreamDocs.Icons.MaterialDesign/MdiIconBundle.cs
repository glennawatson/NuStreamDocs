// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>Static lookup over the generated Material Design Icon catalogue.</summary>
public static class MdiIconBundle
{
    /// <summary>Gets the number of icons in the generated catalogue.</summary>
    public static int Count => MdiIconData.Count;

    /// <summary>Tries to resolve <paramref name="name"/> to its UTF-8 SVG bytes against the generated catalogue.</summary>
    /// <param name="name">UTF-8 icon name (no <c>material-</c> prefix, no surrounding colons).</param>
    /// <param name="svg">UTF-8 SVG bytes on hit.</param>
    /// <returns>True when the icon is in the catalogue.</returns>
    public static bool TryGet(ReadOnlySpan<byte> name, out ReadOnlySpan<byte> svg) =>
        MdiIconData.TryGet(name, out svg);
}
