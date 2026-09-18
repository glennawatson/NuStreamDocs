// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Icons.FontAwesome;

/// <summary>Builder-extension surface for <see cref="FontAwesomePlugin"/>.</summary>
public static class DocBuilderFontAwesomeExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="FontAwesomePlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseFontAwesome() => builder.UsePlugin(new FontAwesomePlugin());

        /// <summary>Registers <see cref="FontAwesomePlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="FontAwesomeOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseFontAwesome(
            Func<FontAwesomeOptions, FontAwesomeOptions> configure)
        {
            var options = configure(FontAwesomeOptions.Default);
            return builder.UsePlugin(new FontAwesomePlugin(options));
        }
    }
}
