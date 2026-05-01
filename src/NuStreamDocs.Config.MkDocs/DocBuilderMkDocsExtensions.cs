// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Builder-extension surface for the mkdocs config reader.
/// </summary>
public static class DocBuilderMkDocsExtensions
{
    /// <summary>Registers <see cref="MkDocsConfigReader"/> with the builder.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMkDocsConfig(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseConfigReader(new MkDocsConfigReader());
    }
}
