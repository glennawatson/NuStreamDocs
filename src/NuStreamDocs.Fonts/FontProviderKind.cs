// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts;

/// <summary>Where a declared font family's files are resolved from at build time.</summary>
public enum FontProviderKind
{
    /// <summary>Resolve the family + weights/styles against the Google Fonts <c>css2</c> API and download the woff2 files.</summary>
    Google,

    /// <summary>Resolve the family against the Fontsource catalogue served from jsDelivr and download the woff2 files.</summary>
    Fontsource,

    /// <summary>Use font files already present in the author's input directory, matched by glob.</summary>
    Local,
}
