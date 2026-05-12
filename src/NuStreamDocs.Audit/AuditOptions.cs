// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Audit;

/// <summary>Configuration for <see cref="AuditPlugin"/>.</summary>
/// <param name="Strict">When true, any finding sets a non-zero process exit code; otherwise findings are reported as warnings only.</param>
/// <param name="Parallelism">Maximum number of pages audited in parallel.</param>
/// <param name="DisabledRules">Lints to skip entirely.</param>
public sealed record AuditOptions(
    bool Strict,
    int Parallelism,
    AuditRule[] DisabledRules)
{
    /// <summary>Default parallelism for the output walk.</summary>
    private static readonly int DefaultParallelism = Math.Max(1, Environment.ProcessorCount);

    /// <summary>Gets the default option set: warnings only, default parallelism, every lint enabled.</summary>
    public static AuditOptions Default { get; } = new(
        Strict: false,
        Parallelism: DefaultParallelism,
        DisabledRules: []);

    /// <summary>Tests whether <paramref name="rule"/> is enabled under these options.</summary>
    /// <param name="rule">The lint to check.</param>
    /// <returns><see langword="true"/> when the rule is not in <see cref="DisabledRules"/>.</returns>
    public bool IsRuleEnabled(AuditRule rule)
    {
        for (var i = 0; i < DisabledRules.Length; i++)
        {
            if (DisabledRules[i] == rule)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Throws when any field is invalid.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When <see cref="Parallelism"/> is non-positive.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Parallelism);
    }
}
