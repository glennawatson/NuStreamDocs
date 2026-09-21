// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Builds TUnit <c>[Arguments]</c> rows from a comparison outcome.</summary>
internal static class TestRowEmitter
{
    /// <summary>Comment marker of a row whose fragment does not yet match its reference.</summary>
    private const string FailsMarker = " - FAILS until the renderer matches the reference";

    /// <summary>
    /// Builds one row. The expected HTML is the chosen reference's output (our own bytes where they match it), except that
    /// <c>mkdocs-better</c> and <c>extension</c> fragments take the MkDocs output and pinned fragments take the pinned HTML.
    /// </summary>
    /// <param name="outcome">The fragment's outcome.</param>
    /// <param name="preferZensical">Whether Zensical rather than MkDocs is the reference.</param>
    /// <returns>The row, ending with a comment naming the fragment.</returns>
    internal static string EmitRow(ParityOutcome outcome, bool preferZensical)
    {
        var mkdocsReference = outcome.Fragment.Expect is { } sidecar && ExpectationKinds.UsesMkDocsReference(sidecar.Kind);
        var source = mkdocsReference || !preferZensical ? outcome.MkDocs : outcome.Zensical;
        var row = ChooseExpectation(outcome, source, mkdocsReference);
        return $"[Arguments({CSharpLiteral.Quote(outcome.Fragment.Markdown)}, {CSharpLiteral.Quote(row.Html)})] // {outcome.Fragment.Id}{row.Marker}";
    }

    /// <summary>Chooses the expected HTML of a row and the comment that follows it.</summary>
    /// <param name="outcome">The fragment's outcome.</param>
    /// <param name="source">HTML of the reference the row follows.</param>
    /// <param name="mkdocsReference">Whether the fragment's sidecar makes MkDocs the reference.</param>
    /// <returns>The expected HTML and its comment.</returns>
    private static RowExpectation ChooseExpectation(ParityOutcome outcome, string? source, bool mkdocsReference)
    {
        var reasonMarker = $" - {outcome.Fragment.Expect?.Kind}: {outcome.Fragment.Expect?.Reason}";
        if (outcome.Status is ParityStatus.Expected or ParityStatus.Undocumented)
        {
            return new(PinnedOrOurs(outcome), reasonMarker);
        }

        return MatchesReference(outcome.Ours, source)
            ? new(outcome.Ours!, mkdocsReference ? reasonMarker : string.Empty)
            : new(ReferenceHtml(source), FailsMarker);
    }

    /// <summary>Gets the pinned HTML of the fragment's sidecar, or our own output when the sidecar has none.</summary>
    /// <param name="outcome">The fragment's outcome.</param>
    /// <returns>The HTML.</returns>
    private static string PinnedOrOurs(ParityOutcome outcome) =>
        outcome.Fragment.Expect?.PinnedHtml is { } pinned ? $"{pinned.TrimEnd('\n')}\n" : outcome.Ours!;

    /// <summary>Determines whether our output equals the reference's after normalization.</summary>
    /// <param name="ours">Our HTML, or <see langword="null"/> when rendering threw.</param>
    /// <param name="reference">The reference's HTML, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> when both exist and are equal.</returns>
    private static bool MatchesReference(string? ours, string? reference) =>
        ours is not null && reference is not null && HtmlNormalizer.Normalize(ours) == HtmlNormalizer.Normalize(reference);

    /// <summary>Gets the reference's HTML without styling markup and with exactly one trailing newline.</summary>
    /// <param name="reference">The reference's HTML, or <see langword="null"/> when it has none.</param>
    /// <returns>The cleaned HTML, or an empty string when the reference has none.</returns>
    private static string ReferenceHtml(string? reference)
    {
        var trimmed = reference is null ? string.Empty : HtmlNormalizer.StripMarkup(reference).TrimEnd('\n');
        return trimmed.Length == 0 ? string.Empty : $"{trimmed}\n";
    }

    /// <summary>The expected HTML of a row and the comment marker that follows it.</summary>
    /// <param name="Html">Expected HTML.</param>
    /// <param name="Marker">Comment text appended after the fragment id.</param>
    private sealed record RowExpectation(string Html, string Marker);
}
