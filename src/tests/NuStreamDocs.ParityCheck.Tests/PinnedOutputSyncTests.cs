// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>
/// Keeps the generated pinned-output rows in step with the sidecars in the corpus.
/// When these fail, regenerate the rows with <c>dotnet run --file tools/Program.cs -- --emit-pinned</c> from the project directory.
/// </summary>
public sealed class PinnedOutputSyncTests
{
    /// <summary>Supplies one case per fragment whose sidecar kind has a pinned-output row.</summary>
    /// <returns>The cases in id order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<SidecarCase> EnumerateSidecars()
    {
        var cases = new List<SidecarCase>();
        foreach (var fragment in ParityPaths.Fragments)
        {
            if (fragment.Expect is { } expect && ExpectationKinds.IsPinned(expect.Kind))
            {
                cases.Add(new(fragment.Id));
            }
        }

        return cases;
    }

    /// <summary>The generated row for a sidecar exists in the method for its kind and carries the fragment Markdown and the pinned HTML.</summary>
    /// <param name="sidecar">The fragment under test.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [MethodDataSource(nameof(EnumerateSidecars))]
    public async Task RowMatchesSidecar(SidecarCase sidecar)
    {
        var fragment = FindFragment(sidecar.Id);
        var expectation = fragment.Expect!;
        var displayName = PinnedOutputNames.DisplayName(fragment.Id, expectation);

        var matching = new List<PinnedRow>();
        foreach (var row in PinnedRow.LoadAll())
        {
            if (row.DisplayName == displayName)
            {
                matching.Add(row);
            }
        }

        await Assert.That(matching.Count).IsEqualTo(1).Because($"'{displayName}' has no generated row; regenerate with --emit-pinned");
        await Assert.That(matching[0].Method).IsEqualTo(PinnedOutputNames.MethodName(expectation.Kind));
        await Assert.That(matching[0].Markdown).IsEqualTo(fragment.Markdown);
        if (expectation.PinnedHtml is { } pinned)
        {
            await Assert.That(matching[0].ExpectedHtml).IsEqualTo(pinned);
        }
    }

    /// <summary>Every generated row belongs to a sidecar that still exists, and every sidecar has its row.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task RowsAndSidecarsCorrespond()
    {
        var expected = new List<string>();
        foreach (var fragment in ParityPaths.Fragments)
        {
            if (fragment.Expect is { } expect && ExpectationKinds.IsPinned(expect.Kind))
            {
                expected.Add(PinnedOutputNames.DisplayName(fragment.Id, expect));
            }
        }

        var actual = new List<string>();
        foreach (var row in PinnedRow.LoadAll())
        {
            actual.Add(row.DisplayName);
        }

        expected.Sort(StringComparer.Ordinal);
        actual.Sort(StringComparer.Ordinal);
        await Assert.That(string.Join('\n', actual)).IsEqualTo(string.Join('\n', expected));
    }

    /// <summary>Finds a fragment of the bundled corpus.</summary>
    /// <param name="id">Fragment id.</param>
    /// <returns>The fragment.</returns>
    /// <exception cref="InvalidOperationException">No fragment has the id.</exception>
    private static Fragment FindFragment(string id)
    {
        foreach (var fragment in ParityPaths.Fragments)
        {
            if (fragment.Id == id)
            {
                return fragment;
            }
        }

        throw new InvalidOperationException($"The corpus has no fragment '{id}'.");
    }
}
