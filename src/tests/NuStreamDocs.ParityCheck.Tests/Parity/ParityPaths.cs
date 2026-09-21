// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Locations of the corpus and the adapter script next to the test binaries.</summary>
internal static class ParityPaths
{
    /// <summary>Gets the directory holding the fragments and sidecars.</summary>
    internal static string FragmentsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "fragments");

    /// <summary>Gets the path of the reference adapter script.</summary>
    internal static string AdapterScript { get; } = Path.Combine(AppContext.BaseDirectory, "reference_adapter.py");

    /// <summary>Gets the fragments bundled with the test binaries, loaded once.</summary>
    internal static Fragment[] Fragments { get; } = FragmentCorpus.Load(FragmentsDirectory);
}
