// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The command line of the parity tool.</summary>
internal sealed class ParityOptions
{
    /// <summary>Number of arguments a flag occupies: the flag itself.</summary>
    private const int FlagWidth = 1;

    /// <summary>Number of arguments an option with one value occupies.</summary>
    private const int OptionWidth = 2;

    /// <summary>Number of arguments <c>--pin</c> occupies: the flag, the kind and the reason.</summary>
    private const int PinWidth = 3;

    /// <summary>Offset of the sidecar reason after <c>--pin</c>.</summary>
    private const int PinReasonOffset = 2;

    /// <summary>Gets the fragment filters; an entry starting with <c>=</c> is an exact id, anything else a case-insensitive substring.</summary>
    internal List<string> Filters { get; } = [];

    /// <summary>Gets or sets the corpus directory.</summary>
    internal string FragmentsDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the feature folder to emit test rows for, or <c>all</c>; <see langword="null"/> when not emitting.</summary>
    internal string? EmitFeature { get; set; }

    /// <summary>Gets or sets the reference engine whose HTML becomes the expected HTML of emitted rows.</summary>
    internal string EmitReference { get; set; } = ReferenceEngineRunner.Zensical;

    /// <summary>Gets or sets the sidecar kind to pin, or <see langword="null"/> when not pinning.</summary>
    internal string? PinKind { get; set; }

    /// <summary>Gets or sets the reason written into pinned sidecars.</summary>
    internal string? PinReason { get; set; }

    /// <summary>Gets or sets a value indicating whether to list fragments without rendering.</summary>
    internal bool List { get; set; }

    /// <summary>Gets or sets a value indicating whether to write machine-readable results.</summary>
    internal bool Json { get; set; }

    /// <summary>Gets or sets a value indicating whether to print outputs for passing and expected fragments too.</summary>
    internal bool ShowAll { get; set; }

    /// <summary>Gets or sets a value indicating whether the Zensical engine runs.</summary>
    internal bool UseZensical { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to regenerate the pinned-output test rows.</summary>
    internal bool EmitPinned { get; set; }

    /// <summary>Gets or sets the message describing an invalid command line, or <see langword="null"/> when it is valid.</summary>
    internal string? Error { get; set; }

    /// <summary>Parses the command line.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="fragmentsDirectory">Corpus directory used unless <c>--fragments</c> overrides it.</param>
    /// <returns>The options; <see cref="Error"/> is set when an argument is not recognized.</returns>
    internal static ParityOptions Parse(string[] args, string fragmentsDirectory)
    {
        var options = new ParityOptions { FragmentsDirectory = fragmentsDirectory };
        var consumed = FlagWidth;
        for (var i = 0; i < args.Length; i += consumed)
        {
            consumed = options.Apply(args, i);
            if (consumed is not 0)
            {
                continue;
            }

            options.Error = $"Unknown argument '{args[i]}'. See README.md.";
            return options;
        }

        return options;
    }

    /// <summary>Determines whether a fragment passes the id filters and the emit-feature filter.</summary>
    /// <param name="fragment">Fragment to test.</param>
    /// <returns><see langword="true"/> when the fragment is selected.</returns>
    internal bool Selects(Fragment fragment)
    {
        var matchesFilters = Filters.Count is 0 || Filters.Exists(filter => filter.StartsWith('=')
            ? fragment.Id.Equals(filter[1..], StringComparison.OrdinalIgnoreCase)
            : fragment.Id.Contains(filter, StringComparison.OrdinalIgnoreCase));
        return matchesFilters && (EmitFeature is null or "all" || fragment.Feature.Equals(EmitFeature, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Applies the argument at <paramref name="i"/> together with the values that follow it.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="i">Index of the argument to apply.</param>
    /// <returns>The number of arguments consumed, or 0 when the argument is not recognized or lacks its values.</returns>
    private int Apply(string[] args, int i)
    {
        if (ApplyFlag(args[i]))
        {
            return FlagWidth;
        }

        if (i + FlagWidth < args.Length && ApplyOption(args[i], args[i + FlagWidth]))
        {
            return OptionWidth;
        }

        if (i + PinReasonOffset < args.Length && args[i] == "--pin")
        {
            PinKind = args[i + FlagWidth];
            PinReason = args[i + PinReasonOffset];
            return PinWidth;
        }

        return 0;
    }

    /// <summary>Applies an argument that takes no value.</summary>
    /// <param name="name">Argument name.</param>
    /// <returns><see langword="true"/> when the argument is a known flag.</returns>
    private bool ApplyFlag(string name)
    {
        switch (name)
        {
            case "--emit-pinned":
            {
                EmitPinned = true;
                return true;
            }

            case "--list":
            {
                List = true;
                return true;
            }

            case "--json":
            {
                Json = true;
                return true;
            }

            case "--show-all":
            {
                ShowAll = true;
                return true;
            }

            case "--no-zensical":
            {
                UseZensical = false;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Applies an argument that takes one value.</summary>
    /// <param name="name">Argument name.</param>
    /// <param name="value">The value that follows it.</param>
    /// <returns><see langword="true"/> when the argument is a known option.</returns>
    private bool ApplyOption(string name, string value)
    {
        switch (name)
        {
            case "--filter":
            {
                Filters.Add(value);
                return true;
            }

            case "--id":
            {
                Filters.Add($"={value}");
                return true;
            }

            case "--fragments":
            {
                FragmentsDirectory = Path.GetFullPath(value);
                return true;
            }

            case "--emit-tests":
            {
                EmitFeature = value;
                return true;
            }

            case "--reference":
            {
                EmitReference = value;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }
}
