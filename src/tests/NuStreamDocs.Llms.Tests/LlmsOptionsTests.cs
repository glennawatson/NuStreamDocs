// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Llms.Tests;

/// <summary>Option fluent mutator, guard clause, and <see cref="DocBuilder"/> registration tests for <see cref="NuStreamDocs.Llms"/>.</summary>
public class LlmsOptionsTests
{
    /// <summary>Every fluent option mutator updates its target property while preserving sibling values.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FluentMutatorsUpdateEveryProperty()
    {
        const int CustomSummaryBudget = 96;
        var summaryBytes = "Fast static docs."u8.ToArray();
        var sectionBytes = "Root"u8.ToArray();

        var custom = LlmsOptions.Default
            .WithEmitFullText(false)
            .WithEmitMarkdownSiblings(false)
            .WithEmitHeadLink(false)
            .WithMaxSummaryBytes(CustomSummaryBudget)
            .WithSiteSummary(summaryBytes)
            .WithDefaultSectionTitle(sectionBytes);

        var spanUpdated = custom
            .WithSiteSummary("Span summary"u8)
            .WithDefaultSectionTitle("Span root"u8);

        await Assert.That(custom.EmitFullText).IsFalse();
        await Assert.That(custom.EmitMarkdownSiblings).IsFalse();
        await Assert.That(custom.EmitHeadLink).IsFalse();
        await Assert.That(custom.MaxSummaryBytes).IsEqualTo(CustomSummaryBudget);
        await Assert.That(Encoding.UTF8.GetString(custom.SiteSummary)).IsEqualTo("Fast static docs.");
        await Assert.That(Encoding.UTF8.GetString(custom.DefaultSectionTitle)).IsEqualTo("Root");
        await Assert.That(Encoding.UTF8.GetString(spanUpdated.SiteSummary)).IsEqualTo("Span summary");
        await Assert.That(Encoding.UTF8.GetString(spanUpdated.DefaultSectionTitle)).IsEqualTo("Span root");
    }

    /// <summary>Null arrays, non-positive byte budgets, and null builder callbacks throw argument exceptions.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InvalidArgumentsThrowExpectedExceptions()
    {
        byte[]? nullBytes = null;
        Func<LlmsOptions, LlmsOptions>? nullConfigure = null;
        DocBuilder builder = new();

        await Assert.That(static () => LlmsOptions.Default.WithMaxSummaryBytes(0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => LlmsOptions.Default.WithMaxSummaryBytes(-1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => LlmsOptions.Default.WithSiteSummary(nullBytes!)).Throws<ArgumentNullException>();
        await Assert.That(() => LlmsOptions.Default.WithDefaultSectionTitle(nullBytes!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.UseLlms(nullConfigure!)).Throws<ArgumentNullException>();
    }
}
