// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.SmartSymbols;

/// <summary>
/// Builder-extension surface for the smart-symbols plugin.
/// </summary>
public static class DocBuilderSmartSymbolsExtensions
{
    /// <summary>Registers <see cref="SmartSymbolsPlugin"/> on <paramref name="builder"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSmartSymbols(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new SmartSymbolsPlugin());
    }
}
