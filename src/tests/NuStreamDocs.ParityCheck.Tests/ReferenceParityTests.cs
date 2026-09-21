// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>
/// Compares every corpus fragment with the reference engines (Zensical first, MkDocs as the fallback).
/// The tests are skipped when no suitable Python interpreter is installed.
/// </summary>
/// <param name="fixture">The session-wide reference results.</param>
[ClassDataSource<ParityFixture>(Shared = SharedType.PerTestSession)]
public sealed class ReferenceParityTests(ParityFixture fixture)
{
    /// <summary>Supplies one case per corpus fragment.</summary>
    /// <returns>The fragment cases in id order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<FragmentCase> EnumerateFragments()
    {
        var fragments = ParityPaths.Fragments;
        var cases = new FragmentCase[fragments.Length];
        for (var i = 0; i < fragments.Length; i++)
        {
            cases[i] = new(fragments[i].Id);
        }

        return cases;
    }

    /// <summary>The fragment renders as the references do, or matches a sidecar that records why it does not.</summary>
    /// <param name="fragment">The fragment under test.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [MethodDataSource(nameof(EnumerateFragments))]
    public async Task FragmentClassificationIsAcceptable(FragmentCase fragment)
    {
        if (fixture.SkipReason is { } reason)
        {
            Skip.Test(reason);
        }

        var outcome = fixture.GetOutcome(fragment.Id);
        await Assert.That(outcome.Fails).IsFalse().Because(outcome.Describe());
    }
}
