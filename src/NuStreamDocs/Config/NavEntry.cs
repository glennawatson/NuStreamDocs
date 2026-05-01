// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config;

/// <summary>
/// Single navigation entry parsed from mkdocs.yml <c>nav:</c>.
/// </summary>
/// <param name="Title">Display title.</param>
/// <param name="Path">Relative source path or URL.</param>
public readonly record struct NavEntry(string Title, string Path);
