// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Read-only context handed to <see cref="IBuildConfigurePlugin.ConfigureAsync"/>.
/// </summary>
/// <param name="InputRoot">Absolute path to the docs root directory.</param>
/// <param name="OutputRoot">Absolute path to the site output directory.</param>
/// <param name="Plugins">Every plugin registered with the builder, in registration order. Plugins use this to discover companion contracts such as <see cref="IHeadExtraProvider"/>.</param>
/// <param name="CrossPageMarkers">Registry plugins write into to declare byte markers that signal a page needs cross-page resolution.</param>
public readonly record struct BuildConfigureContext(
    DirectoryPath InputRoot,
    DirectoryPath OutputRoot,
    IPlugin[] Plugins,
    CrossPageMarkerRegistry CrossPageMarkers)
{
    /// <summary>Gets a value indicating whether the build emits pretty URLs (<c>foo/index.html</c>) instead of flat <c>foo.html</c>.</summary>
    public bool UseDirectoryUrls { get; init; }

    /// <summary>Gets the UTF-8-encoded configured site name; an empty array when none configured.</summary>
    public byte[] SiteName { get; init; } = [];

    /// <summary>Gets the UTF-8-encoded canonical site URL; an empty array when none configured.</summary>
    public byte[] SiteUrl { get; init; } = [];

    /// <summary>Gets the UTF-8-encoded site-wide author name; an empty array when none configured.</summary>
    public byte[] SiteAuthor { get; init; } = [];
}
