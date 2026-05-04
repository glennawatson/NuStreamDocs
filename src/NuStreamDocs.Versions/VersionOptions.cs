// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Versions;

/// <summary>Configuration for the versions plugin.</summary>
/// <remarks>
/// <see cref="Aliases"/> is stored as UTF-8 bytes per the project's byte-first pipeline rule.
/// String-shaped construction is via <see cref="VersionOptionsExtensions.WithAliases(VersionOptions, ApiCompatString[])"/>
/// and friends, which encode once at the boundary.
/// </remarks>
/// <param name="Version">Identifier for the version this build represents.</param>
/// <param name="Title">Human-readable selector label.</param>
/// <param name="Aliases">UTF-8 aliases that point at this version (e.g. <c>latest</c>).</param>
public sealed record VersionOptions(string Version, string Title, byte[][] Aliases)
{
    /// <summary>Initializes a new instance of the <see cref="VersionOptions"/> class with no aliases.</summary>
    /// <param name="version">Version identifier.</param>
    /// <param name="title">Selector label.</param>
    public VersionOptions(string version, string title)
        : this(version, title, [])
    {
    }

    /// <summary>Returns options where the aliases include <c>latest</c>.</summary>
    /// <param name="version">Version identifier.</param>
    /// <param name="title">Selector label.</param>
    /// <returns>Options with the <c>latest</c> alias attached.</returns>
    public static VersionOptions Latest(string version, string title) =>
        new(version, title, [[.. "latest"u8]]);

    /// <summary>Throws when any required field is empty or whitespace.</summary>
    /// <exception cref="ArgumentException">When a required field is null, empty, or whitespace.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentNullException.ThrowIfNull(Aliases);
    }

    /// <summary>Materializes the option set as a <see cref="VersionEntry"/>.</summary>
    /// <returns>The matching entry.</returns>
    public VersionEntry ToEntry() => new(Version, Title, Aliases);
}
