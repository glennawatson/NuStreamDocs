// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Caching;

/// <summary>One entry in the build manifest, recording what was rendered previously so the next build can skip unchanged pages.</summary>
/// <param name="RelativePath">Page path relative to the input root, forward-slashed.</param>
/// <param name="ContentHash">16-byte ASCII lowercase hex digest of the source UTF-8 bytes.</param>
/// <param name="OutputLengthBytes">Length of the previously emitted output file.</param>
public readonly record struct ManifestEntry(
    FilePath RelativePath,
    byte[] ContentHash,
    long OutputLengthBytes);
