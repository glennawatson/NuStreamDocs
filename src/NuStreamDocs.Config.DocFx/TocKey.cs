// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.DocFx;

/// <summary>Recognized key kinds in a docfx toc.yml item.</summary>
internal enum TocKey
{
    /// <summary>Unknown / unrecognized key.</summary>
    Unknown,

    /// <summary>The <c>name</c> key (display title).</summary>
    Name,

    /// <summary>The <c>href</c> key (target path or sub-toc).</summary>
    Href,

    /// <summary>The <c>homepage</c> key (landing page for a directory ref).</summary>
    Homepage
}
