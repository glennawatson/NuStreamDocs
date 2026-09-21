// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The comparison of one fragment against both reference engines.</summary>
/// <param name="Fragment">The fragment that was compared.</param>
/// <param name="Status">How our output relates to the references.</param>
/// <param name="Note">Explanation of the status, or <see langword="null"/> when there is nothing to add.</param>
/// <param name="Ours">Our rendered HTML, or <see langword="null"/> when rendering threw.</param>
/// <param name="MkDocs">MkDocs' HTML, or <see langword="null"/> when it has none.</param>
/// <param name="Zensical">Zensical's HTML, or <see langword="null"/> when it has none.</param>
[DebuggerDisplay("{Status} {Fragment.Id}")]
internal sealed record ParityOutcome(Fragment Fragment, ParityStatus Status, string? Note, string? Ours, string? MkDocs, string? Zensical)
{
    /// <summary>Text shown for an output that does not exist.</summary>
    private const string None = "(none)";

    /// <summary>Gets a value indicating whether the status fails the run.</summary>
    internal bool Fails => Status.Fails();

    /// <summary>Makes control characters visible on one line.</summary>
    /// <param name="text">Text to show.</param>
    /// <returns>The text with line breaks and tabs spelled out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Show(string text) =>
        text.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);

    /// <summary>Describes the outcome for a failure message.</summary>
    /// <returns>The status, the note and the normalized outputs.</returns>
    internal string Describe()
    {
        var ours = Ours is null ? string.Empty : HtmlNormalizer.Normalize(Ours);
        var zensical = Zensical is null ? None : HtmlNormalizer.Normalize(Zensical);
        var mkdocs = MkDocs is null ? None : HtmlNormalizer.Normalize(MkDocs);
        var note = Note is null ? string.Empty : $"  {Note}";
        return $"{Status.Label()} {Fragment.Id}{note}\n  md       : {Show(Fragment.Markdown)}\n  ours     : {Show(ours)}\n  zensical : {Show(zensical)}\n  mkdocs   : {Show(mkdocs)}";
    }
}
