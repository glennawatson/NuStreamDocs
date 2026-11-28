// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.I18n;

/// <summary>Builder-extension surface for <see cref="I18nPlugin"/>.</summary>
public static class DocBuilderI18nExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="I18nPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseI18n() => builder.UsePlugin(new I18nPlugin());

        /// <summary>Registers <see cref="I18nPlugin"/> with caller-customized options.</summary>
        /// <param name="configure">Function receiving <see cref="I18nOptions.Default"/> and returning customized options.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseI18n(Func<I18nOptions, I18nOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new I18nPlugin(configure(I18nOptions.Default)));
        }

        /// <summary>Registers <see cref="I18nPlugin"/> with caller-supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseI18n(in I18nOptions options) =>
            builder.UsePlugin(new I18nPlugin(options));
    }
}
