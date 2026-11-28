// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Social;

/// <summary>Builder-extension surface for <see cref="SocialPlugin"/>.</summary>
public static class DocBuilderSocialExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="SocialPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSocial() => builder.UsePlugin(new SocialPlugin());

        /// <summary>Registers <see cref="SocialPlugin"/> with caller-customized options.</summary>
        /// <param name="configure">Function receiving <see cref="SocialOptions.Default"/> and returning customized options.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseSocial(Func<SocialOptions, SocialOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new SocialPlugin(configure(SocialOptions.Default)));
        }

        /// <summary>Registers <see cref="SocialPlugin"/> with caller-supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSocial(in SocialOptions options) =>
            builder.UsePlugin(new SocialPlugin(options));
    }
}
