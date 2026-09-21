// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>How a fragment's rendered output relates to the reference engines.</summary>
internal enum ParityStatus
{
    /// <summary>Our output equals Zensical's.</summary>
    Pass = 0,

    /// <summary>Our output equals MkDocs but not Zensical and no sidecar explains it.</summary>
    MkDocsOnly = 1,

    /// <summary>Our output equals neither reference and no sidecar explains it.</summary>
    Diff = 2,

    /// <summary>Our output equals MkDocs and a <c>mkdocs-better</c> sidecar records why MkDocs is right.</summary>
    MkDocsBetter = 3,

    /// <summary>Our output equals MkDocs and an <c>extension</c> sidecar records the out-of-scope Zensical extension.</summary>
    Extension = 4,

    /// <summary>A <c>deviation</c> or <c>quirk</c> sidecar matches the pinned output.</summary>
    Expected = 5,

    /// <summary>An <c>undocumented</c> sidecar matches the pinned output.</summary>
    Undocumented = 6,

    /// <summary>A <c>known-bug</c> sidecar matches the pinned output.</summary>
    KnownBug = 7,

    /// <summary>Our output now equals the primary reference, so the sidecar is obsolete.</summary>
    StaleExpect = 8,

    /// <summary>Our output differs from the pinned output of the sidecar.</summary>
    Changed = 9,

    /// <summary>The renderer threw.</summary>
    Error = 10,

    /// <summary>The primary reference produced no output for the fragment.</summary>
    NoReference = 11,
}
