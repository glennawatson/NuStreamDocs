// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Export;

/// <summary>Captured per-page content harvested during the scan phase for single-file HTML export.</summary>
/// <param name="SortKey">Normalized relative path used for deterministic ordering.</param>
/// <param name="ServedUrl">Served URL bytes of the page.</param>
/// <param name="SectionId">Unique HTML anchor ID assigned to this chapter.</param>
/// <param name="Title">Plain-text UTF-8 chapter title.</param>
/// <param name="BodyHtml">Extracted inner HTML of the page's primary content container.</param>
[System.Diagnostics.DebuggerDisplay("ExportPageEntry: {SortKey}")]
internal readonly record struct ExportPageEntry(
    string SortKey,
    byte[] ServedUrl,
    byte[] SectionId,
    byte[] Title,
    byte[] BodyHtml);
