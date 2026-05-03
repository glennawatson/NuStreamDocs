// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Read-only context handed to <see cref="IDocPlugin.OnFinalizeAsync"/>.
/// </summary>
/// <param name="OutputRoot">Absolute path to the site output directory.</param>
public readonly record struct PluginFinalizeContext(DirectoryPath OutputRoot);
