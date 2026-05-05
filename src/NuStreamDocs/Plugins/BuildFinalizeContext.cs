// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Read-only context handed to <see cref="IBuildFinalizePlugin.FinalizeAsync"/>.
/// </summary>
/// <param name="OutputRoot">Absolute path to the site output directory.</param>
/// <param name="Plugins">Every plugin registered with the builder; finalizers consult sibling state through their owning plugin instance.</param>
public readonly record struct BuildFinalizeContext(
    DirectoryPath OutputRoot,
    IPlugin[] Plugins);
