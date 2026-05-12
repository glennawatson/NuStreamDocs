// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Resolves a declared <see cref="FontFace"/> into the concrete woff2 files to ship.</summary>
public interface IFontProvider
{
    /// <summary>Resolves <paramref name="face"/> into one <see cref="FontResource"/> per weight/style/subset, downloading or reading the woff2 bytes.</summary>
    /// <param name="face">The declared face.</param>
    /// <param name="requestedSubsets">UTF-8 subset names to fetch (or the single name <c>all</c>); ignored by the local provider.</param>
    /// <param name="cache">Download cache (unused by the local provider).</param>
    /// <param name="inputRoot">The build's input directory, used to resolve local globs.</param>
    /// <param name="subsetUsage">
    /// When non-null (an <c>auto</c> face), a Unicode-block bitset; the provider drops — before downloading — any subset whose
    /// <c>unicode-range</c> covers no marked block. Ignored by providers without per-subset <c>unicode-range</c> data.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved resources.</returns>
    ValueTask<FontResource[]> ResolveAsync(
        FontFace face,
        byte[][] requestedSubsets,
        FontDownloadCache cache,
        DirectoryPath inputRoot,
        bool[]? subsetUsage,
        CancellationToken cancellationToken);
}
