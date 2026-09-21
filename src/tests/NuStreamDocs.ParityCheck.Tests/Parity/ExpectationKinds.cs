// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The sidecar kinds a fragment can carry.</summary>
internal static class ExpectationKinds
{
    /// <summary>Documented deviation from the references; the renderer output is pinned.</summary>
    internal const string Deviation = "deviation";

    /// <summary>The renderer follows CommonMark, the references differ, and no documentation lists it; the output is pinned.</summary>
    internal const string Undocumented = "undocumented";

    /// <summary>Reference behavior that is not worth matching; the output is pinned.</summary>
    internal const string Quirk = "quirk";

    /// <summary>Known defect kept visible without failing the run; the output is pinned.</summary>
    internal const string KnownBug = "known-bug";

    /// <summary>Zensical is buggy or does not implement the behavior and MkDocs is right.</summary>
    internal const string MkDocsBetter = "mkdocs-better";

    /// <summary>Zensical's output comes from an extension beyond basic Markdown.</summary>
    internal const string Extension = "extension";

    /// <summary>Gets every kind a sidecar may declare.</summary>
    internal static string[] All { get; } = [Deviation, Undocumented, Quirk, KnownBug, MkDocsBetter, Extension];

    /// <summary>Gets the kinds that have their pinned output checked without any reference engine.</summary>
    internal static string[] Pinned { get; } = [Deviation, Extension, MkDocsBetter];

    /// <summary>Determines whether the kind names an out-of-scope Zensical behavior for which MkDocs is the reference.</summary>
    /// <param name="kind">Sidecar kind.</param>
    /// <returns><see langword="true"/> for <see cref="MkDocsBetter"/> and <see cref="Extension"/>.</returns>
    internal static bool UsesMkDocsReference(string kind) => kind is MkDocsBetter or Extension;

    /// <summary>Determines whether the kind has a generated pinned-output test.</summary>
    /// <param name="kind">Sidecar kind.</param>
    /// <returns><see langword="true"/> for <see cref="Deviation"/>, <see cref="Extension"/> and <see cref="MkDocsBetter"/>.</returns>
    internal static bool IsPinned(string kind) => Array.IndexOf(Pinned, kind) >= 0;
}
