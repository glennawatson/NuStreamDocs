// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Read-only context handed to <see cref="IBuildDiscoverPlugin.DiscoverAsync"/>.
/// </summary>
/// <param name="InputRoot">Absolute path to the docs root directory; plugins emit synthesized markdown pages under this directory before page enumeration begins.</param>
/// <param name="OutputRoot">Absolute path to the site output directory.</param>
/// <param name="Plugins">Every plugin registered with the builder, in registration order.</param>
public readonly record struct BuildDiscoverContext(
    DirectoryPath InputRoot,
    DirectoryPath OutputRoot,
    IPlugin[] Plugins)
{
    /// <summary>Gets a value indicating whether the build emits pretty URLs (<c>foo/index.html</c>).</summary>
    public bool UseDirectoryUrls { get; init; }
}
