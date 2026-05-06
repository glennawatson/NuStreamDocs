// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Tests for the space-separated keyword and operator factories.</summary>
public class SpaceSeparatedFactoryTests
{
    /// <summary>Round-trip a known list through CreateFromSpaceSeparated and verify Contains.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ByteKeywordSet_CreateFromSpaceSeparated_round_trips_known_words()
    {
        var set = ByteKeywordSet.CreateFromSpaceSeparated("if else for while return"u8);

        await Assert.That(set.Contains("if"u8)).IsTrue();
        await Assert.That(set.Contains("else"u8)).IsTrue();
        await Assert.That(set.Contains("for"u8)).IsTrue();
        await Assert.That(set.Contains("while"u8)).IsTrue();
        await Assert.That(set.Contains("return"u8)).IsTrue();
        await Assert.That(set.Contains("missing"u8)).IsFalse();
        await Assert.That(set.Contains("IF"u8)).IsFalse();
    }

    /// <summary>Multiple consecutive whitespace characters are treated as a single separator.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ByteKeywordSet_CreateFromSpaceSeparated_skips_empty_runs()
    {
        var set = ByteKeywordSet.CreateFromSpaceSeparated("  if\t\telse  for "u8);

        await Assert.That(set.Contains("if"u8)).IsTrue();
        await Assert.That(set.Contains("else"u8)).IsTrue();
        await Assert.That(set.Contains("for"u8)).IsTrue();
    }

    /// <summary>The case-insensitive variant matches both casings.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ByteKeywordSet_CreateFromSpaceSeparatedIgnoreCase_matches_both_casings()
    {
        var set = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase("select from where"u8);

        await Assert.That(set.Contains("select"u8)).IsTrue();
        await Assert.That(set.Contains("SELECT"u8)).IsTrue();
        await Assert.That(set.Contains("Where"u8)).IsTrue();
        await Assert.That(set.Contains("missing"u8)).IsFalse();
    }

    /// <summary>SplitLongestFirst orders alternation entries by descending length.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task OperatorAlternationFactory_SplitLongestFirst_orders_by_descending_length()
    {
        var ops = OperatorAlternationFactory.SplitLongestFirst("+ ..= == += -"u8);

        await Assert.That(ops.Length).IsEqualTo(5);
        await Assert.That(ops[0].AsSpan().SequenceEqual("..="u8)).IsTrue();
        await Assert.That(ops[1].AsSpan().SequenceEqual("=="u8)).IsTrue();
        await Assert.That(ops[2].AsSpan().SequenceEqual("+="u8)).IsTrue();
        await Assert.That(ops[3].Length).IsEqualTo(1);
        await Assert.That(ops[4].Length).IsEqualTo(1);
    }

    /// <summary>FirstBytesOf produces a SearchValues containing every leading byte.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task OperatorAlternationFactory_FirstBytesOf_covers_every_leading_byte()
    {
        var ops = OperatorAlternationFactory.SplitLongestFirst("+ -= == ?"u8);
        var first = OperatorAlternationFactory.FirstBytesOf(ops);

        await Assert.That(first.Contains((byte)'+')).IsTrue();
        await Assert.That(first.Contains((byte)'-')).IsTrue();
        await Assert.That(first.Contains((byte)'=')).IsTrue();
        await Assert.That(first.Contains((byte)'?')).IsTrue();
        await Assert.That(first.Contains((byte)'/')).IsFalse();
    }
}
