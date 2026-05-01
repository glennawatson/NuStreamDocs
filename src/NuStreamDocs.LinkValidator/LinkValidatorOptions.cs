// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator;

/// <summary>Configuration for <see cref="LinkValidatorPlugin"/>.</summary>
/// <param name="StrictInternal">When true, internal-link diagnostics fail the build.</param>
/// <param name="StrictExternal">When true, external-link diagnostics fail the build (and the network checker runs).</param>
/// <param name="Parallelism">Maximum parallel readers / page checks.</param>
/// <param name="External">External validator settings (used only when <paramref name="StrictExternal"/> is true).</param>
public sealed record LinkValidatorOptions(
    bool StrictInternal,
    bool StrictExternal,
    int Parallelism,
    ExternalLinkValidatorOptions External)
{
    /// <summary>Default parallelism for the corpus walk.</summary>
    private static readonly int DefaultParallelism = Math.Max(1, Environment.ProcessorCount);

    /// <summary>Gets the default option set: warnings only, default parallelism, default external settings.</summary>
    public static LinkValidatorOptions Default { get; } = new(
        StrictInternal: false,
        StrictExternal: false,
        Parallelism: DefaultParallelism,
        External: ExternalLinkValidatorOptions.Default);

    /// <summary>Throws when any field is invalid.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When <see cref="Parallelism"/> is non-positive.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Parallelism);
        ArgumentNullException.ThrowIfNull(External);
        External.Validate();
    }
}
