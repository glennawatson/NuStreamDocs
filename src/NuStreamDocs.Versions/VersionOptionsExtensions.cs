// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Versions;

/// <summary>String / span construction helpers for the byte-shaped <see cref="VersionOptions"/> record.</summary>
/// <remarks>
/// Encodes the inputs once at construction so the <c>versions.json</c> writer flows pure UTF-8.
/// Callers building from configuration files (which produce strings) reach for the string overloads;
/// callers with byte-literal sources can pass <c>"..."u8.ToArray()</c> directly.
/// </remarks>
public static class VersionOptionsExtensions
{
    /// <summary>Replaces the alias list with <paramref name="aliases"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="aliases">Alias strings.</param>
    /// <returns>The updated options.</returns>
    public static VersionOptions WithAliases(this VersionOptions options, params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options with { Aliases = aliases.EncodeUtf8Array() };
    }

    /// <summary>Replaces the alias list with the supplied UTF-8 alias bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="aliases">Alias bytes (one entry per alias).</param>
    /// <returns>The updated options.</returns>
    public static VersionOptions WithAliases(this VersionOptions options, params byte[][] aliases)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(aliases);
        return options with { Aliases = aliases };
    }

    /// <summary>Appends <paramref name="aliases"/> to the existing alias list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="aliases">Additional alias strings.</param>
    /// <returns>The updated options.</returns>
    public static VersionOptions AddAliases(this VersionOptions options, params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(aliases);
        return aliases.Length is 0
            ? options
            : options with { Aliases = ArrayJoiner.Concat(options.Aliases, aliases.EncodeUtf8Array()) };
    }

    /// <summary>Appends UTF-8 <paramref name="aliases"/> to the existing alias list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="aliases">Additional alias bytes.</param>
    /// <returns>The updated options.</returns>
    public static VersionOptions AddAliases(this VersionOptions options, params byte[][] aliases)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(aliases);
        return aliases.Length is 0
            ? options
            : options with { Aliases = ArrayJoiner.Concat(options.Aliases, aliases) };
    }

    /// <summary>Appends a single UTF-8 alias (e.g. a <c>"..."u8</c> literal) to the existing alias list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="alias">UTF-8 alias bytes.</param>
    /// <returns>The updated options.</returns>
    public static VersionOptions AddAliases(this VersionOptions options, ReadOnlySpan<byte> alias)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options with { Aliases = ArrayJoiner.Concat(options.Aliases, [alias.ToArray()]) };
    }

    /// <summary>Empties the alias list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static VersionOptions ClearAliases(this VersionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options with { Aliases = [] };
    }
}
