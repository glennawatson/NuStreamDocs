// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Read-only context handed to <see cref="IDocPlugin.OnConfigureAsync"/>.
/// </summary>
/// <param name="InputRoot">Absolute path to the docs root directory.</param>
/// <param name="OutputRoot">Absolute path to the site output directory.</param>
/// <param name="Plugins">Every plugin registered with the builder, in registration order. Theme plugins use this to discover companion contracts such as <see cref="IHeadExtraProvider"/>.</param>
public readonly record struct PluginConfigureContext(
    DirectoryPath InputRoot,
    DirectoryPath OutputRoot,
    IDocPlugin[] Plugins)
{
    /// <summary>Gets a value indicating whether the build emits pretty URLs (<c>foo/index.html</c>) instead of flat <c>foo.html</c>.</summary>
    /// <remarks>Mirrors the builder's <c>UseDirectoryUrls</c> switch; theme plugins consult this when computing canonical / OpenGraph URLs.</remarks>
    public bool UseDirectoryUrls { get; init; }

    /// <summary>Gets the UTF-8-encoded configured site name; an empty array when none configured.</summary>
    /// <remarks>Encode-once at builder time; consumers slice into HTML attributes / sitemap entries directly without re-encoding.</remarks>
    public byte[] SiteName { get; init; } = [];

    /// <summary>Gets the UTF-8-encoded canonical site URL; an empty array when none configured.</summary>
    /// <remarks>Used by sitemap, theme, and link-rewriter plugins to compute absolute URLs.</remarks>
    public byte[] SiteUrl { get; init; } = [];

    /// <summary>Gets the UTF-8-encoded site-wide author name; an empty array when none configured.</summary>
    /// <remarks>Used by theme plugins as the fallback for the per-page <c>&lt;meta name="author"&gt;</c> when no front-matter <c>author:</c> is set.</remarks>
    public byte[] SiteAuthor { get; init; } = [];
}
