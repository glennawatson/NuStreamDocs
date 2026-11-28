// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Playgrounds;

/// <summary>Fluent builder helpers for <see cref="PlaygroundsOptions"/>.</summary>
public static class PlaygroundsOptionsExtensions
{
    /// <summary>Extension members for <see cref="PlaygroundsOptions"/>.</summary>
    /// <param name="options">Source options record.</param>
    extension(PlaygroundsOptions options)
    {
        /// <summary>Returns a copy toggling the interactive HTTP Try-It drawer.</summary>
        /// <param name="enabled"><c>true</c> to render the interactive Send Request panel; otherwise, <c>false</c>.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        public PlaygroundsOptions WithInteractiveTryIt(bool enabled)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options with { EnableInteractiveTryIt = enabled };
        }

        /// <summary>Returns a copy toggling emission of the built-in CSS and JS assets.</summary>
        /// <param name="enabled"><c>true</c> to emit and link built-in playground assets; otherwise, <c>false</c>.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        public PlaygroundsOptions WithAssets(bool enabled)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options with { IncludeAssets = enabled };
        }
    }
}
