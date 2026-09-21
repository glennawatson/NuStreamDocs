// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Classifies how a fragment's output relates to the references. Zensical is the primary reference; MkDocs stands in only when Zensical is not run.</summary>
internal static class ParityClassifier
{
    /// <summary>Alternative spelling of the known-bug sidecar kind.</summary>
    private const string LegacyBugKind = "bug";

    /// <summary>Classifies one fragment.</summary>
    /// <param name="fragment">The fragment and its sidecar.</param>
    /// <param name="ours">Our render result.</param>
    /// <param name="mkdocs">MkDocs' results.</param>
    /// <param name="zensical">Zensical's results; unavailable when the engine was not run.</param>
    /// <returns>The outcome.</returns>
    internal static ParityOutcome Evaluate(Fragment fragment, RenderResult ours, ReferenceEngine mkdocs, ReferenceEngine zensical)
    {
        var mk = mkdocs.Html(fragment.Id);
        var zn = zensical.Html(fragment.Id);
        if (ours.Html is null)
        {
            return Outcome(fragment, ParityStatus.Error, $"ours threw {ours.Error}", null, mk, zn);
        }

        var primary = zensical.Available ? zn : mk;
        if (primary is null)
        {
            return Outcome(fragment, ParityStatus.NoReference, DescribeMissingReference(fragment, mkdocs, zensical), ours.Html, mk, zn);
        }

        var comparison = new Comparison(fragment, ours.Html, mk, zn, primary, zensical.Available);
        return fragment.Expect is { } expect ? EvaluateSidecar(comparison, expect) : EvaluateWithoutSidecar(comparison);
    }

    /// <summary>Builds an outcome, describing the sidecar when no note is given.</summary>
    /// <param name="fragment">The fragment.</param>
    /// <param name="status">Status of the fragment.</param>
    /// <param name="note">Explanation, or <see langword="null"/> to describe the sidecar.</param>
    /// <param name="ours">Our HTML.</param>
    /// <param name="mkdocs">MkDocs' HTML.</param>
    /// <param name="zensical">Zensical's HTML.</param>
    /// <returns>The outcome.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ParityOutcome Outcome(Fragment fragment, ParityStatus status, string? note, string? ours, string? mkdocs, string? zensical) =>
        new(fragment, status, note ?? (fragment.Expect is { } expectation ? $"[{expectation.Kind}] {expectation.Reason}" : null), ours, mkdocs, zensical);

    /// <summary>Explains why the primary reference has no output for the fragment.</summary>
    /// <param name="fragment">The fragment.</param>
    /// <param name="mkdocs">MkDocs' results.</param>
    /// <param name="zensical">Zensical's results.</param>
    /// <returns>The explanation.</returns>
    private static string DescribeMissingReference(Fragment fragment, ReferenceEngine mkdocs, ReferenceEngine zensical)
    {
        var engine = zensical.Available ? zensical : mkdocs;
        var name = zensical.Available ? "Zensical" : "MkDocs";
        return engine.Available ? $"{name} failed: {engine.Results[fragment.Id].Error}" : engine.Label;
    }

    /// <summary>Classifies a fragment that has a sidecar.</summary>
    /// <param name="comparison">The comparison of our output with the references.</param>
    /// <param name="expect">The fragment's sidecar.</param>
    /// <returns>The outcome.</returns>
    private static ParityOutcome EvaluateSidecar(Comparison comparison, Expectation expect)
    {
        var needsMkDocs = ExpectationKinds.UsesMkDocsReference(expect.Kind);
        if (comparison.MatchesPrimary && !(needsMkDocs && !comparison.ZensicalAvailable))
        {
            return comparison.Outcome(ParityStatus.StaleExpect, $"matches {comparison.PrimaryName} now; delete the .expect ({expect.Kind}: {expect.Reason})");
        }

        if (expect.PinnedHtml is { } pinned && HtmlNormalizer.Normalize(pinned) != comparison.OursNormalized)
        {
            return comparison.Outcome(ParityStatus.Changed, $"output differs from the pinned .expect HTML ({expect.Kind}: {expect.Reason})");
        }

        return needsMkDocs ? EvaluateMkDocsReference(comparison, expect) : comparison.Outcome(PinnedStatus(expect.Kind), null);
    }

    /// <summary>Classifies a <c>mkdocs-better</c> or <c>extension</c> fragment, whose output must equal MkDocs.</summary>
    /// <param name="comparison">The comparison of our output with the references.</param>
    /// <param name="expect">The fragment's sidecar.</param>
    /// <returns>The outcome.</returns>
    private static ParityOutcome EvaluateMkDocsReference(Comparison comparison, Expectation expect)
    {
        if (!comparison.MatchesMkDocs)
        {
            return comparison.Outcome(ParityStatus.Diff, $"[{expect.Kind}] matches neither Zensical nor MkDocs ({expect.Reason})");
        }

        return comparison.Outcome(expect.Kind == ExpectationKinds.MkDocsBetter ? ParityStatus.MkDocsBetter : ParityStatus.Extension, null);
    }

    /// <summary>Gets the status of a fragment whose pinned sidecar output matches.</summary>
    /// <param name="kind">Sidecar kind.</param>
    /// <returns>The status for the kind.</returns>
    private static ParityStatus PinnedStatus(string kind) =>
        kind switch
        {
            LegacyBugKind or ExpectationKinds.KnownBug => ParityStatus.KnownBug,
            ExpectationKinds.Undocumented => ParityStatus.Undocumented,
            _ => ParityStatus.Expected,
        };

    /// <summary>Classifies a fragment that has no sidecar.</summary>
    /// <param name="comparison">The comparison of our output with the references.</param>
    /// <returns>The outcome.</returns>
    private static ParityOutcome EvaluateWithoutSidecar(Comparison comparison)
    {
        if (comparison.MatchesPrimary)
        {
            return comparison.Outcome(ParityStatus.Pass, null);
        }

        return comparison.MatchesMkDocs
            ? comparison.Outcome(ParityStatus.MkDocsOnly, "matches MkDocs, not Zensical; fix the renderer or add a mkdocs-better / extension sidecar")
            : comparison.Outcome(ParityStatus.Diff, comparison.ReferencesDiffer ? "Zensical and MkDocs differ from each other too" : null);
    }

    /// <summary>Our output next to both references, with the equality checks the classification needs.</summary>
    private sealed class Comparison
    {
        /// <summary>The fragment being compared.</summary>
        private readonly Fragment _fragment;

        /// <summary>Our HTML.</summary>
        private readonly string _ours;

        /// <summary>MkDocs' HTML, or <see langword="null"/>.</summary>
        private readonly string? _mkdocs;

        /// <summary>Zensical's HTML, or <see langword="null"/>.</summary>
        private readonly string? _zensical;

        /// <summary>Initializes a new instance of the <see cref="Comparison"/> class.</summary>
        /// <param name="fragment">The fragment being compared.</param>
        /// <param name="ours">Our HTML.</param>
        /// <param name="mkdocs">MkDocs' HTML, or <see langword="null"/>.</param>
        /// <param name="zensical">Zensical's HTML, or <see langword="null"/>.</param>
        /// <param name="primary">The primary reference's HTML.</param>
        /// <param name="zensicalAvailable">Whether Zensical ran, which makes it the primary reference.</param>
        internal Comparison(Fragment fragment, string ours, string? mkdocs, string? zensical, string primary, bool zensicalAvailable)
        {
            _fragment = fragment;
            _ours = ours;
            _mkdocs = mkdocs;
            _zensical = zensical;
            ZensicalAvailable = zensicalAvailable;
            PrimaryName = zensicalAvailable ? "Zensical" : "MkDocs";
            OursNormalized = HtmlNormalizer.Normalize(ours);
            MatchesPrimary = OursNormalized == HtmlNormalizer.Normalize(primary);
            MatchesMkDocs = mkdocs is not null && OursNormalized == HtmlNormalizer.Normalize(mkdocs);
            ReferencesDiffer = zensical is not null && mkdocs is not null && HtmlNormalizer.Normalize(zensical) != HtmlNormalizer.Normalize(mkdocs);
        }

        /// <summary>Gets a value indicating whether Zensical ran.</summary>
        internal bool ZensicalAvailable { get; }

        /// <summary>Gets the name of the primary reference.</summary>
        internal string PrimaryName { get; }

        /// <summary>Gets our normalized HTML.</summary>
        internal string OursNormalized { get; }

        /// <summary>Gets a value indicating whether our output equals the primary reference.</summary>
        internal bool MatchesPrimary { get; }

        /// <summary>Gets a value indicating whether our output equals MkDocs.</summary>
        internal bool MatchesMkDocs { get; }

        /// <summary>Gets a value indicating whether Zensical and MkDocs differ from each other.</summary>
        internal bool ReferencesDiffer { get; }

        /// <summary>Builds the outcome for this comparison.</summary>
        /// <param name="status">Status of the fragment.</param>
        /// <param name="note">Explanation, or <see langword="null"/> to describe the sidecar.</param>
        /// <returns>The outcome.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ParityOutcome Outcome(ParityStatus status, string? note) => ParityClassifier.Outcome(_fragment, status, note, _ours, _mkdocs, _zensical);
    }
}
