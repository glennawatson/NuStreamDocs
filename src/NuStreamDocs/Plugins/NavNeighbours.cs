// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Adjacent-page metadata returned by <see cref="INavNeighboursProvider.GetNeighbours(FilePath)"/>.
/// </summary>
/// <param name="PreviousPath">Source-relative path of the previous leaf page in nav order; empty when there is none.</param>
/// <param name="PreviousTitle">Display title of the previous page; empty when there is none.</param>
/// <param name="NextPath">Source-relative path of the next leaf page in nav order; empty when there is none.</param>
/// <param name="NextTitle">Display title of the next page; empty when there is none.</param>
public readonly record struct NavNeighbours(
    FilePath PreviousPath,
    byte[] PreviousTitle,
    FilePath NextPath,
    byte[] NextTitle)
{
    /// <summary>Gets the empty value (no previous, no next).</summary>
    public static NavNeighbours None { get; } = new(default, [], default, []);
}
