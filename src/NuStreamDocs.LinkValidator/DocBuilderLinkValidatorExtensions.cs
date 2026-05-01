// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.LinkValidator;

/// <summary>Builder extension that registers <see cref="LinkValidatorPlugin"/>.</summary>
public static class DocBuilderLinkValidatorExtensions
{
    /// <summary>Registers <see cref="LinkValidatorPlugin"/> with default options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLinkValidator(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new LinkValidatorPlugin());
    }

    /// <summary>Registers <see cref="LinkValidatorPlugin"/> with the supplied options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLinkValidator(this DocBuilder builder, LinkValidatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new LinkValidatorPlugin(options));
    }

    /// <summary>Registers <see cref="LinkValidatorPlugin"/> with the supplied options and logger.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger to receive validation diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLinkValidator(this DocBuilder builder, LinkValidatorOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new LinkValidatorPlugin(options, httpClientFactory: null, logger));
    }
}
