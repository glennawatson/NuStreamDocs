// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Byte-level case-insensitive label classifier.</summary>
public class LocatorLabelTests
{
    /// <summary>Lowercase pandoc page synonyms classify as <see cref="LocatorKind.Page"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PageSynonymsClassify()
    {
        await Assert.That(LocatorLabel.Classify("p"u8)).IsEqualTo(LocatorKind.Page);
        await Assert.That(LocatorLabel.Classify("pp"u8)).IsEqualTo(LocatorKind.Page);
        await Assert.That(LocatorLabel.Classify("page"u8)).IsEqualTo(LocatorKind.Page);
    }

    /// <summary>Mixed-case input is normalized to the same kind.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UppercaseFolds()
    {
        await Assert.That(LocatorLabel.Classify("CHAPTER"u8)).IsEqualTo(LocatorKind.Chapter);
        await Assert.That(LocatorLabel.Classify("Section"u8)).IsEqualTo(LocatorKind.Section);
    }

    /// <summary>Unknown labels fall through to <see cref="LocatorKind.Other"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownIsOther() =>
        await Assert.That(LocatorLabel.Classify("foo"u8)).IsEqualTo(LocatorKind.Other);

    /// <summary>Empty span is <see cref="LocatorKind.Other"/> (no specific label).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyIsOther() =>
        await Assert.That(LocatorLabel.Classify(default)).IsEqualTo(LocatorKind.Other);

    /// <summary>Schedule and article synonyms classify.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScheduleAndArticleClassify()
    {
        await Assert.That(LocatorLabel.Classify("sch"u8)).IsEqualTo(LocatorKind.Schedule);
        await Assert.That(LocatorLabel.Classify("schedules"u8)).IsEqualTo(LocatorKind.Schedule);
        await Assert.That(LocatorLabel.Classify("art"u8)).IsEqualTo(LocatorKind.Article);
        await Assert.That(LocatorLabel.Classify("articles"u8)).IsEqualTo(LocatorKind.Article);
    }
}
