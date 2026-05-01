// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>Discriminator for <see cref="ExtraAssetSource"/>.</summary>
public enum ExtraAssetSourceKind
{
    /// <summary>A file path on disk; bytes read at configure time.</summary>
    File,

    /// <summary>Caller-supplied UTF-8 bytes shipped under a chosen filename.</summary>
    Inline,

    /// <summary>An embedded resource pulled out of an assembly.</summary>
    Embedded,

    /// <summary>An external URL; no asset is shipped, only a <c>&lt;link&gt;</c> / <c>&lt;script&gt;</c> tag.</summary>
    Url,
}
