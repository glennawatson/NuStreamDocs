// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Optimize;

/// <summary>Compression formats the optimizer plugin can emit alongside each output file.</summary>
[Flags]
public enum OptimizeFormats
{
    /// <summary>No precompressed output.</summary>
    None = 0,

    /// <summary>Emit a gzip-compressed sibling (<c>.gz</c>).</summary>
    Gzip = 1,

    /// <summary>Emit a brotli-compressed sibling (<c>.br</c>).</summary>
    Brotli = 2,

    /// <summary>Emit both gzip and brotli siblings.</summary>
    Both = Gzip | Brotli
}
