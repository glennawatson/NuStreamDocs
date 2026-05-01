// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Toc;

/// <summary>
/// Builder-extension surface for <see cref="TocPlugin"/>.
/// </summary>
public static class DocBuilderTocExtensions
{
    /// <summary>Registers <see cref="TocPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseToc(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new TocPlugin());
    }

    /// <summary>Registers <see cref="TocPlugin"/> with caller-supplied options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseToc(this DocBuilder builder, in TocOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new TocPlugin(options));
    }

    /// <summary>Registers <see cref="TocPlugin"/> with caller-supplied options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger forwarded to the plugin.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseToc(this DocBuilder builder, in TocOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new TocPlugin(options, logger));
    }
}
