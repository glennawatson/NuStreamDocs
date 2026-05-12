// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader;

/// <summary>Per-build context handed to an <see cref="IContentLoader"/>.</summary>
/// <param name="InputRoot">Absolute path to the docs input root; loaders that read local files resolve relative paths against it.</param>
public readonly record struct ContentLoaderContext(DirectoryPath InputRoot)
{
    /// <summary>Gets a value indicating whether the build emits directory-style URLs.</summary>
    public bool UseDirectoryUrls { get; init; }
}
