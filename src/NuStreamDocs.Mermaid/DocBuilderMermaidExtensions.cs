// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Mermaid;

/// <summary>Builder-extension surface for the mermaid plugin.</summary>
public static class DocBuilderMermaidExtensions
{
    /// <summary>Registers <see cref="MermaidPlugin"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMermaid(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new MermaidPlugin());
    }
}
