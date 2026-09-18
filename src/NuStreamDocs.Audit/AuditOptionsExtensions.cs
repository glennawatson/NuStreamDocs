// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Audit;

/// <summary>Fluent mutators for <see cref="AuditOptions"/>.</summary>
public static class AuditOptionsExtensions
{
    /// <summary>Extension members for <c>AuditOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(AuditOptions options)
    {
        /// <summary>Returns a copy with strict mode enabled (findings fail the build).</summary>
        /// <returns>The updated options.</returns>
        public AuditOptions WithStrict() =>
            options with { Strict = true };

        /// <summary>Returns a copy with the maximum page-audit parallelism set.</summary>
        /// <param name="parallelism">Maximum parallel page audits.</param>
        /// <returns>The updated options.</returns>
        public AuditOptions WithParallelism(int parallelism) =>
            options with { Parallelism = parallelism };

        /// <summary>Returns a copy with <paramref name="rule"/> added to the disabled set.</summary>
        /// <param name="rule">The lint to disable.</param>
        /// <returns>The updated options.</returns>
        public AuditOptions Disable(AuditRule rule)
        {
            if (!options.IsRuleEnabled(rule))
            {
                return options;
            }

            AuditRule[] disabled = [.. options.DisabledRules, rule];
            return options with { DisabledRules = disabled };
        }
    }
}
