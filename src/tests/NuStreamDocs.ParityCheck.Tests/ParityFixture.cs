// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TUnit.Core.Interfaces;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>
/// Renders the whole corpus once per test session with our renderer and both reference engines and classifies every fragment.
/// The session is skipped when no suitable Python interpreter exists and fails when the reference environment cannot be prepared.
/// </summary>
[DebuggerDisplay("{_outcomes.Count} outcomes, skip={SkipReason}")]
public sealed class ParityFixture : IAsyncInitializer
{
    /// <summary>Classified fragments keyed by id.</summary>
    private readonly Dictionary<string, ParityOutcome> _outcomes = [with(StringComparer.Ordinal)];

    /// <summary>Gets the reason the reference comparison cannot run, or <see langword="null"/> when it ran.</summary>
    public string? SkipReason { get; private set; }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        var probe = await PythonProbe.FindAsync().ConfigureAwait(false);
        if (probe.Interpreter is null)
        {
            SkipReason = probe.DescribeMissing();
            return;
        }

        var venvDirectory = ReferenceVenvLocation.Resolve(AppContext.BaseDirectory);
        var venvInterpreter = await ReferenceVenv.EnsureAsync(probe.Interpreter, venvDirectory).ConfigureAwait(false);

        var fragments = ParityPaths.Fragments;
        var mkdocsTask = ReferenceEngineRunner.RunAsync(venvInterpreter, ParityPaths.AdapterScript, ReferenceEngineRunner.MkDocs, fragments);
        var zensicalTask = ReferenceEngineRunner.RunAsync(venvInterpreter, ParityPaths.AdapterScript, ReferenceEngineRunner.Zensical, fragments);
        var mkdocs = await mkdocsTask.ConfigureAwait(false);
        var zensical = await zensicalTask.ConfigureAwait(false);

        for (var i = 0; i < fragments.Length; i++)
        {
            _outcomes[fragments[i].Id] = ParityClassifier.Evaluate(fragments[i], OursRenderer.Render(fragments[i].Markdown), mkdocs, zensical);
        }
    }

    /// <summary>Gets the classified outcome of a fragment.</summary>
    /// <param name="id">Fragment id.</param>
    /// <returns>The outcome.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ParityOutcome GetOutcome(string id) => _outcomes[id];
}
