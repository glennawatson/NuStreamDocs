// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Display and pass/fail rules for <see cref="ParityStatus"/>.</summary>
internal static class ParityStatusExtensions
{
    /// <summary>Members of <see cref="ParityStatus"/> for reporting and pass/fail decisions.</summary>
    /// <param name="status">The status.</param>
    extension(ParityStatus status)
    {
        /// <summary>Gets the upper-case label used in reports.</summary>
        /// <returns>The label, such as <c>MKDOCS-BETTER</c>.</returns>
        internal string Label() =>
            status switch
            {
                ParityStatus.Pass => "PASS",
                ParityStatus.MkDocsOnly => "MKDOCS-ONLY",
                ParityStatus.Diff => "DIFF",
                ParityStatus.MkDocsBetter => "MKDOCS-BETTER",
                ParityStatus.Extension => "EXTENSION",
                ParityStatus.Expected => "EXPECTED",
                ParityStatus.Undocumented => "UNDOCUMENTED",
                ParityStatus.KnownBug => "KNOWN-BUG",
                ParityStatus.StaleExpect => "STALE-EXPECT",
                ParityStatus.Changed => "CHANGED",
                ParityStatus.Error => "ERROR",
                ParityStatus.NoReference => "NO-REFERENCE",
                _ => status.ToString(),
            };

        /// <summary>Determines whether the status fails the run.</summary>
        /// <returns><see langword="false"/> for pass, mkdocs-better, extension, expected, undocumented and known-bug; otherwise <see langword="true"/>.</returns>
        internal bool Fails() =>
            status is not (ParityStatus.Pass or ParityStatus.MkDocsBetter or ParityStatus.Extension
                or ParityStatus.Expected or ParityStatus.Undocumented or ParityStatus.KnownBug);
    }
}
