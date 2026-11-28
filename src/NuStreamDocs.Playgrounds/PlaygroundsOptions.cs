// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Playgrounds;

/// <summary>Configuration options for <see cref="PlaygroundsPlugin"/>.</summary>
[System.Diagnostics.DebuggerDisplay("PlaygroundsOptions: TryIt={EnableInteractiveTryIt}")]
public sealed record PlaygroundsOptions
{
    /// <summary>Gets the shared default playground options.</summary>
    public static PlaygroundsOptions Default { get; } = new();

    /// <summary>Gets a value indicating whether <c>http-request</c> blocks include the interactive browser Try-It drawer.</summary>
    public bool EnableInteractiveTryIt { get; init; } = true;

    /// <summary>Gets a value indicating whether to emit and link the built-in playground stylesheet and script.</summary>
    public bool IncludeAssets { get; init; } = true;
}
