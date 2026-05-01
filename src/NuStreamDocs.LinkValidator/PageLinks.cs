// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Per-page link inventory captured during the single corpus-build pass.
/// </summary>
/// <remarks>
/// One instance per emitted <c>.html</c> file. Stored once in
/// <see cref="ValidationCorpus"/>; both the internal validator and
/// the external validator read from this single record so no
/// caller ever re-walks the disk tree.
/// </remarks>
/// <param name="PageUrl">Site-relative URL of the page (forward-slashed, with <c>.html</c>).</param>
/// <param name="InternalLinks">Relative <c>href</c>s + page-local <c>#fragment</c>s.</param>
/// <param name="ExternalLinks">Absolute <c>http://</c> / <c>https://</c> hrefs.</param>
/// <param name="AnchorIds">Heading <c>id</c>s on this page; frozen for O(1) lookup during the validate phase.</param>
public sealed record PageLinks(
    string PageUrl,
    string[] InternalLinks,
    string[] ExternalLinks,
    FrozenSet<string> AnchorIds);
