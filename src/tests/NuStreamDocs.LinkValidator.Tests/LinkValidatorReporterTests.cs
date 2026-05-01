// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Direct unit tests for the static <c>LinkValidatorReporter</c> helper extracted out of the plugin.</summary>
public class LinkValidatorReporterTests
{
    /// <summary>HasFatal returns false on an empty stream.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HasFatalEmpty() => await Assert.That(LinkValidatorReporter.HasFatal([])).IsFalse();

    /// <summary>HasFatal returns true when any diagnostic is severity-error.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HasFatalDetectsError()
    {
        LinkDiagnostic[] diags = [Warn("a"), Err("b"), Warn("c")];
        await Assert.That(LinkValidatorReporter.HasFatal(diags)).IsTrue();
    }

    /// <summary>HasFatal returns false when every diagnostic is a warning.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HasFatalAllWarnings() =>
        await Assert.That(LinkValidatorReporter.HasFatal([Warn("a"), Warn("b")])).IsFalse();

    /// <summary>HasFatal rejects null diagnostics.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HasFatalRejectsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => LinkValidatorReporter.HasFatal(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>AdjustSeverity demotes errors to warnings when strict is false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AdjustSeverityDemotesWhenLax() =>
        await Assert.That(LinkValidatorReporter.AdjustSeverity(Err("x"), strict: false).Severity)
            .IsEqualTo(LinkSeverity.Warning);

    /// <summary>AdjustSeverity preserves errors when strict is true.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AdjustSeverityKeepsWhenStrict() =>
        await Assert.That(LinkValidatorReporter.AdjustSeverity(Err("x"), strict: true).Severity)
            .IsEqualTo(LinkSeverity.Error);

    /// <summary>Tally splits broken from warnings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TallyCountsCorrectly()
    {
        var (broken, warnings) = LinkValidatorReporter.Tally([Err("a"), Warn("b"), Err("c"), Warn("d"), Warn("e")]);
        await Assert.That(broken).IsEqualTo(2);
        await Assert.That(warnings).IsEqualTo(3);
    }

    /// <summary>Tally on an empty stream returns zeros.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TallyEmpty()
    {
        var (broken, warnings) = LinkValidatorReporter.Tally([]);
        await Assert.That(broken).IsEqualTo(0);
        await Assert.That(warnings).IsEqualTo(0);
    }

    /// <summary>Merge splices both validator outputs and demotes per the matching strict flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MergeDemotesPerFlag()
    {
        LinkDiagnostic[] internalDiags = [Err("i1"), Err("i2")];
        LinkDiagnostic[] externalDiags = [Err("e1")];
        var merged = LinkValidatorReporter.Merge(internalDiags, externalDiags, strictInternal: false, strictExternal: true);
        await Assert.That(merged.Length).IsEqualTo(3);
        await Assert.That(merged[0].Severity).IsEqualTo(LinkSeverity.Warning);
        await Assert.That(merged[1].Severity).IsEqualTo(LinkSeverity.Warning);
        await Assert.That(merged[2].Severity).IsEqualTo(LinkSeverity.Error);
    }

    /// <summary>Merge rejects null arrays.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MergeRejectsNullArrays()
    {
        Assert.Throws<ArgumentNullException>(static () => LinkValidatorReporter.Merge(null!, [], false, false));
        var ex = Assert.Throws<ArgumentNullException>(static () => LinkValidatorReporter.Merge([], null!, false, false));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Builds an error-severity diagnostic for the given page.</summary>
    /// <param name="sourcePage">Page name.</param>
    /// <returns>Diagnostic.</returns>
    private static LinkDiagnostic Err(string sourcePage) =>
        new(sourcePage, "/x", LinkSeverity.Error, "broken");

    /// <summary>Builds a warning-severity diagnostic for the given page.</summary>
    /// <param name="sourcePage">Page name.</param>
    /// <returns>Diagnostic.</returns>
    private static LinkDiagnostic Warn(string sourcePage) =>
        new(sourcePage, "/x", LinkSeverity.Warning, "soft");
}
