// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Prints comparison outcomes for the parity tool.</summary>
internal static class ParityReport
{
    /// <summary>Longest excerpt of an output printed on one line.</summary>
    private const int ExcerptLimit = 400;

    /// <summary>Characters of context printed before the first difference.</summary>
    private const int ExcerptLeadIn = 120;

    /// <summary>Width of the status column; the longest status label is 13 characters.</summary>
    private const int StatusColumnWidth = 13;

    /// <summary>Text shown for an output that does not exist.</summary>
    private const string None = "(none)";

    /// <summary>Prints one outcome; passing and expected outcomes are a single line unless <paramref name="showAll"/> is set.</summary>
    /// <param name="output">Destination.</param>
    /// <param name="outcome">The outcome to print.</param>
    /// <param name="showAll">Whether to print the outputs of quiet outcomes too.</param>
    internal static void Print(TextWriter output, ParityOutcome outcome, bool showAll)
    {
        var note = outcome.Note is null ? string.Empty : $"  {outcome.Note}";
        output.WriteLine($"{outcome.Status.Label().PadRight(StatusColumnWidth)} {outcome.Fragment.Id}{note}");
        if (outcome.Status is ParityStatus.Pass or ParityStatus.Expected && !showAll)
        {
            return;
        }

        PrintOutputs(output, outcome);
    }

    /// <summary>Prints the whole report followed by the summary line.</summary>
    /// <param name="output">Destination.</param>
    /// <param name="engines">Engine labels: MkDocs first, Zensical second.</param>
    /// <param name="outcomes">All outcomes.</param>
    /// <param name="showAll">Whether to print the outputs of quiet outcomes too.</param>
    /// <returns>The number of failing outcomes.</returns>
    internal static int PrintAll(TextWriter output, EnginePair engines, List<ParityOutcome> outcomes, bool showAll)
    {
        output.WriteLine($"mkdocs   : {engines.MkDocs.Label}");
        output.WriteLine($"zensical : {engines.Zensical.Label}");
        output.WriteLine();
        SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
        var failing = 0;
        foreach (var outcome in outcomes)
        {
            Print(output, outcome, showAll);
            var label = outcome.Status.Label();
            counts[label] = counts.GetValueOrDefault(label) + 1;
            failing += outcome.Fails ? 1 : 0;
        }

        output.WriteLine();
        var summary = new List<string>(counts.Count);
        foreach (var count in counts)
        {
            summary.Add($"{count.Key} {count.Value}");
        }

        output.WriteLine($"{outcomes.Count} fragments: {string.Join(", ", summary)}");
        output.WriteLine(failing == 0 ? "OK" : $"FAILED: {failing} unexpected");
        return failing;
    }

    /// <summary>Writes the outcomes as indented JSON.</summary>
    /// <param name="stream">Destination.</param>
    /// <param name="engines">Both engines.</param>
    /// <param name="outcomes">All outcomes.</param>
    internal static void WriteJson(Stream stream, EnginePair engines, List<ParityOutcome> outcomes)
    {
        using var writer = new Utf8JsonWriter(stream, new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        writer.WriteStartObject();
        writer.WriteString("mkdocs", engines.MkDocs.Label);
        writer.WriteString("zensical", engines.Zensical.Label);
        writer.WriteStartArray("fragments");
        foreach (var outcome in outcomes)
        {
            writer.WriteStartObject();
            writer.WriteString("id", outcome.Fragment.Id);
            writer.WriteString("status", outcome.Status.Label());
            writer.WriteString("note", outcome.Note);
            writer.WriteString("markdown", outcome.Fragment.Markdown);
            writer.WriteString("ours", outcome.Ours);
            writer.WriteString("mkdocs", outcome.MkDocs);
            writer.WriteString("zensical", outcome.Zensical);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Prints the Markdown and the normalized outputs of both references next to ours, windowed around the first difference.</summary>
    /// <param name="output">Destination.</param>
    /// <param name="outcome">The outcome to print.</param>
    private static void PrintOutputs(TextWriter output, ParityOutcome outcome)
    {
        output.WriteLine($"  md       : {ParityOutcome.Show(Excerpt(outcome.Fragment.Markdown, 0))}");
        var ours = outcome.Ours is null ? string.Empty : HtmlNormalizer.Normalize(outcome.Ours);
        var mkdocs = outcome.MkDocs is null ? null : HtmlNormalizer.Normalize(outcome.MkDocs);
        var zensical = outcome.Zensical is null ? null : HtmlNormalizer.Normalize(outcome.Zensical);
        var anchor = zensical ?? mkdocs;
        var at = anchor is null ? 0 : FirstDifference(ours, anchor);
        output.WriteLine($"  ours     : {ParityOutcome.Show(Excerpt(ours, at))}");
        output.WriteLine($"  zensical : {ExcerptOrNone(zensical, at)}");
        output.WriteLine($"  mkdocs   : {(mkdocs is not null && mkdocs == zensical ? "same as zensical" : ExcerptOrNone(mkdocs, at))}");
    }

    /// <summary>Formats an optional output for one report line.</summary>
    /// <param name="text">Normalized output, or <see langword="null"/> when the engine has none.</param>
    /// <param name="at">Index of the first difference.</param>
    /// <returns>The windowed output, or a marker when there is none.</returns>
    private static string ExcerptOrNone(string? text, int at) => text is null ? None : ParityOutcome.Show(Excerpt(text, at));

    /// <summary>Returns <paramref name="text"/> unless it is long, in which case a window around the difference position.</summary>
    /// <param name="text">Text to shorten.</param>
    /// <param name="at">Index of the first difference.</param>
    /// <returns>The text or the window.</returns>
    private static string Excerpt(string text, int at)
    {
        if (text.Length <= ExcerptLimit)
        {
            return text;
        }

        var start = Math.Max(0, at - ExcerptLeadIn);
        var end = Math.Min(text.Length, start + ExcerptLimit);
        return (start > 0 ? $"...[{start}] " : string.Empty) + text[start..end] + (end < text.Length ? " ..." : string.Empty);
    }

    /// <summary>Finds the index where two strings first differ.</summary>
    /// <param name="left">First string.</param>
    /// <param name="right">Second string.</param>
    /// <returns>The index of the first differing character, or the shorter length.</returns>
    private static int FirstDifference(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var i = 0;
        while (i < length && left[i] == right[i])
        {
            i++;
        }

        return i;
    }
}
