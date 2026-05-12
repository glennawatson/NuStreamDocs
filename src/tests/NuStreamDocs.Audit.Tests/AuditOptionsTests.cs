// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Audit.Tests;

/// <summary>Coverage for <see cref="AuditOptions"/> and <see cref="AuditOptionsExtensions"/>.</summary>
public class AuditOptionsTests
{
    /// <summary>The default options enable every lint, are warn-only, and validate.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsAreWarnOnlyWithEveryRule()
    {
        var options = AuditOptions.Default;
        options.Validate();
        await Assert.That(options.Strict).IsFalse();
        await Assert.That(options.IsRuleEnabled(AuditRule.ImageMissingAlt)).IsTrue();
        await Assert.That(options.IsRuleEnabled(AuditRule.RenderBlockingScript)).IsTrue();
    }

    /// <summary>The fluent mutators compose without affecting the source.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MutatorsCompose()
    {
        var options = AuditOptions.Default
            .WithStrict()
            .WithParallelism(2)
            .Disable(AuditRule.PositiveTabIndex)
            .Disable(AuditRule.PositiveTabIndex);

        await Assert.That(options.Strict).IsTrue();
        await Assert.That(options.Parallelism).IsEqualTo(2);
        await Assert.That(options.IsRuleEnabled(AuditRule.PositiveTabIndex)).IsFalse();
        await Assert.That(options.DisabledRules.Length).IsEqualTo(1);
        await Assert.That(AuditOptions.Default.Strict).IsFalse();
    }

    /// <summary>Validation rejects a non-positive parallelism.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateRejectsBadParallelism()
    {
        var options = AuditOptions.Default.WithParallelism(0);
        await Assert.That(options.Validate).Throws<ArgumentOutOfRangeException>();
    }
}
