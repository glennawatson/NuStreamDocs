// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>
/// Implements the Australian Guide to Legal Citation (4th ed)
/// formatting rules. All output is written directly to a UTF-8 sink —
/// no intermediate <see cref="string"/> per format call.
/// </summary>
public sealed class Aglc4Style : ICitationStyle
{
    /// <summary>Gets the singleton instance — the style is stateless.</summary>
    public static Aglc4Style Instance { get; } = new();

    /// <inheritdoc/>
    public byte[] Name => "AGLC4"u8.ToArray();

    /// <inheritdoc/>
    public void WriteInText(CitationEntry entry, int footnoteNumber, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(writer);
        Aglc4Writer.WriteBytes("[^bib-"u8, writer);
        Aglc4Writer.WriteString(entry.Id, writer);
        Aglc4Writer.WriteBytes("]"u8, writer);
    }

    /// <inheritdoc/>
    public void WriteFootnote(CitationEntry entry, CitationLocator locator, ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(writer);
        WriteCore(entry, writer);
        if (!locator.HasValue)
        {
            return;
        }

        Aglc4Writer.WriteBytes(" "u8, writer);
        Aglc4Pinpoint.Write(locator, source, writer);
    }

    /// <inheritdoc/>
    public void WriteBibliography(CitationEntry entry, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(writer);
        WriteCore(entry, writer);
    }

    /// <summary>Writes the bare citation (no locator) per AGLC4 rules for the entry's type.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteCore(CitationEntry entry, IBufferWriter<byte> writer)
    {
        if (TryWritePrint(entry, writer))
        {
            return;
        }

        if (TryWriteLegal(entry, writer))
        {
            return;
        }

        Aglc4Generic.Write(entry, writer);
    }

    /// <summary>Dispatches the print-style entry types (book / chapter / articles / report / thesis / webpage).</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a print-style formatter handled the entry.</returns>
    private static bool TryWritePrint(CitationEntry entry, IBufferWriter<byte> writer)
    {
        var formatter = SelectPrintFormatter(entry.Type);
        if (formatter is null)
        {
            return false;
        }

        formatter(entry, writer);
        return true;
    }

    /// <summary>Selects the right print-style formatter for the entry type, or <c>null</c> when none applies.</summary>
    /// <param name="type">Entry type.</param>
    /// <returns>Formatter delegate or null.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Single switch expression dispatching by entry type.")]
    private static Action<CitationEntry, IBufferWriter<byte>>? SelectPrintFormatter(EntryType type) => type switch
    {
        EntryType.Book or EntryType.Chapter => Aglc4Books.Write,
        EntryType.ArticleJournal or EntryType.ArticleMagazine or EntryType.ArticleNewspaper or EntryType.Article => Aglc4Articles.Write,
        EntryType.Report => Aglc4Reports.Write,
        EntryType.Thesis => Aglc4Thesis.Write,
        EntryType.Webpage => Aglc4Webpages.Write,
        _ => null,
    };

    /// <summary>Dispatches the legal entry types (case / legislation / treaty).</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a legal formatter handled the entry.</returns>
    private static bool TryWriteLegal(CitationEntry entry, IBufferWriter<byte> writer)
    {
        var formatter = SelectLegalFormatter(entry.Type);
        if (formatter is null)
        {
            return false;
        }

        formatter(entry, writer);
        return true;
    }

    /// <summary>Selects the right legal-style formatter for the entry type, or <c>null</c> when none applies.</summary>
    /// <param name="type">Entry type.</param>
    /// <returns>Formatter delegate or null.</returns>
    private static Action<CitationEntry, IBufferWriter<byte>>? SelectLegalFormatter(EntryType type) => type switch
    {
        EntryType.LegalCase => Aglc4Cases.Write,
        EntryType.Legislation => Aglc4Legislation.Write,
        EntryType.Treaty => Aglc4Treaties.Write,
        _ => null,
    };
}
