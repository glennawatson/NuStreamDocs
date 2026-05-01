// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Tags;

/// <summary>Configuration for <see cref="TagsPlugin"/>.</summary>
/// <param name="OutputSubdirectory">Subdirectory under the site root where tag pages are written; defaults to <c>tags</c>.</param>
/// <param name="IndexFileName">File name of the all-tags landing page; defaults to <c>index.html</c>.</param>
public readonly record struct TagsOptions(
    string OutputSubdirectory,
    string IndexFileName)
{
    /// <summary>Gets the default option set (<c>tags/index.html</c>).</summary>
    public static TagsOptions Default { get; } = new("tags", "index.html");
}
