// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.LinkValidator;

/// <summary>Builder extension that registers <see cref="LinkValidatorPlugin"/>.</summary>
public static class DocBuilderLinkValidatorExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="LinkValidatorPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLinkValidator() => builder.UsePlugin(new LinkValidatorPlugin());

        /// <summary>Registers <see cref="LinkValidatorPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLinkValidator(LinkValidatorOptions options) =>
            builder.UsePlugin(new LinkValidatorPlugin(options));

        /// <summary>Registers <see cref="LinkValidatorPlugin"/> with the supplied options and logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger to receive validation diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLinkValidator(LinkValidatorOptions options, ILogger logger) =>
            builder.UsePlugin(new LinkValidatorPlugin(options, null, logger));
    }
}
