// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Autorefs;
using NuStreamDocs.Building;

namespace NuStreamDocs.Xrefs;

/// <summary>Builder extension that registers <see cref="XrefsPlugin"/>.</summary>
public static class DocBuilderXrefsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">The builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="XrefsPlugin"/> with default options and a fresh registry.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseXrefs() => builder.UsePlugin(new XrefsPlugin());

        /// <summary>Registers <see cref="XrefsPlugin"/> with the supplied options and a fresh registry.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseXrefs(XrefsOptions options) =>
            builder.UsePlugin(new XrefsPlugin(new(), options));

        /// <summary>Registers <see cref="XrefsPlugin"/> sharing <paramref name="registry"/> with another plugin (typically <c>AutorefsPlugin</c>).</summary>
        /// <param name="registry">Shared autorefs registry.</param>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseXrefs(AutorefsRegistry registry, XrefsOptions options) =>
            builder.UsePlugin(new XrefsPlugin(registry, options));
    }
}
