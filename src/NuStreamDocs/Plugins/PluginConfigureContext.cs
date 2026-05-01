// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Config;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Read-only context handed to <see cref="IDocPlugin.OnConfigureAsync"/>.
/// </summary>
/// <param name="Config">Parsed mkdocs/builder config.</param>
/// <param name="InputRoot">Absolute path to the docs root directory.</param>
/// <param name="OutputRoot">Absolute path to the site output directory.</param>
/// <param name="Plugins">Every plugin registered with the builder, in registration order. Theme plugins use this to discover companion contracts such as <see cref="IHeadExtraProvider"/>.</param>
public readonly record struct PluginConfigureContext(
    MkDocsConfig Config,
    string InputRoot,
    string OutputRoot,
    IDocPlugin[] Plugins);
