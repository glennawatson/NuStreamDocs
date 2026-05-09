// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Optional resolver consulted by the icon-shortcode rewriter before falling back to the default font-ligature span.
/// </summary>
public interface IIconResolver
{
    /// <summary>Tries to resolve <paramref name="iconName"/> and write its inline markup to <paramref name="writer"/>.</summary>
    /// <param name="iconName">UTF-8 icon name bytes; matches what appears between the trailing <c>-</c> of the family prefix and the closing <c>:</c>.</param>
    /// <param name="writer">UTF-8 sink to receive the inline icon markup on hit.</param>
    /// <returns>True when the resolver claimed this icon name and wrote markup to <paramref name="writer"/>.</returns>
    bool TryResolve(ReadOnlySpan<byte> iconName, IBufferWriter<byte> writer);
}
