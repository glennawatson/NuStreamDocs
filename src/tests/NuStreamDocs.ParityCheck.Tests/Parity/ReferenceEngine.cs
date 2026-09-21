// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The results one reference engine produced for a set of fragments.</summary>
/// <param name="Name">Engine name: <c>mkdocs</c> or <c>zensical</c>.</param>
/// <param name="Label">Engine name and version, or the reason the engine is unavailable.</param>
/// <param name="UnavailableReason">Why the engine did not run, or <see langword="null"/> when it did.</param>
/// <param name="Results">Render results keyed by fragment id.</param>
[DebuggerDisplay("{Name}: {Label}")]
internal sealed record ReferenceEngine(string Name, string Label, string? UnavailableReason, Dictionary<string, RenderResult> Results)
{
    /// <summary>Gets a value indicating whether the engine ran.</summary>
    internal bool Available => UnavailableReason is null;

    /// <summary>Creates a placeholder for an engine that was deliberately not run.</summary>
    /// <param name="name">Engine name.</param>
    /// <param name="reason">Why the engine was not run.</param>
    /// <returns>An unavailable engine with no results.</returns>
    internal static ReferenceEngine Disabled(string name, string reason) => new(name, reason, reason, []);

    /// <summary>Gets the HTML the engine produced for a fragment.</summary>
    /// <param name="id">Fragment id.</param>
    /// <returns>The HTML, or <see langword="null"/> when the engine has none for the fragment.</returns>
    internal string? Html(string id) => Results.TryGetValue(id, out var result) ? result.Html : null;
}
