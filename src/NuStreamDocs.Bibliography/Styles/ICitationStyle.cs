// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles;

/// <summary>
/// Renders citations to a UTF-8 sink. Implementations format the in-text
/// marker, the footnote body, and the bibliography list entry.
/// </summary>
public interface ICitationStyle
{
    /// <summary>Gets the style's display name as UTF-8 bytes (e.g. <c>"AGLC4"u8</c>).</summary>
    ReadOnlySpan<byte> Name { get; }

    /// <summary>Writes the style-dependent in-text reference (e.g. footnote ref, "(Smith 2024)").</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="footnoteNumber">1-based footnote number assigned by the plugin.</param>
    /// <param name="writer">UTF-8 sink.</param>
    void WriteInText(CitationEntry entry, int footnoteNumber, IBufferWriter<byte> writer);

    /// <summary>Writes the footnote-body text for a single citation reference. Markdown is allowed in the output.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="locator">Pinpoint locator; may be <see cref="CitationLocator.None"/>.</param>
    /// <param name="source">Original markdown source span the locator's offsets point into.</param>
    /// <param name="writer">UTF-8 sink.</param>
    void WriteFootnote(CitationEntry entry, CitationLocator locator, ReadOnlySpan<byte> source, IBufferWriter<byte> writer);

    /// <summary>Writes one bibliography list entry. Markdown is allowed.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    void WriteBibliography(CitationEntry entry, IBufferWriter<byte> writer);
}
