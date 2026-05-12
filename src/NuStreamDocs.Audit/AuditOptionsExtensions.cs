// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Audit;

/// <summary>Fluent mutators for <see cref="AuditOptions"/>.</summary>
public static class AuditOptionsExtensions
{
    /// <summary>Returns a copy with strict mode enabled (findings fail the build).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static AuditOptions WithStrict(this AuditOptions options) =>
        options with { Strict = true };

    /// <summary>Returns a copy with the maximum page-audit parallelism set.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="parallelism">Maximum parallel page audits.</param>
    /// <returns>The updated options.</returns>
    public static AuditOptions WithParallelism(this AuditOptions options, int parallelism) =>
        options with { Parallelism = parallelism };

    /// <summary>Returns a copy with <paramref name="rule"/> added to the disabled set.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="rule">The lint to disable.</param>
    /// <returns>The updated options.</returns>
    public static AuditOptions Disable(this AuditOptions options, AuditRule rule)
    {
        if (!options.IsRuleEnabled(rule))
        {
            return options;
        }

        AuditRule[] disabled = [.. options.DisabledRules, rule];
        return options with { DisabledRules = disabled };
    }
}
