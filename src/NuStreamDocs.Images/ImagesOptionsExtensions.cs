// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Images;

/// <summary>Fluent configuration helpers for <see cref="ImagesOptions"/>.</summary>
public static class ImagesOptionsExtensions
{
    /// <summary>Extension members for <see cref="ImagesOptions"/>.</summary>
    /// <param name="options">Options to customize.</param>
    extension(in ImagesOptions options)
    {
        /// <summary>Marks the first <paramref name="count"/> images on every page as <c>loading="eager"</c> so hero images are not deferred.</summary>
        /// <param name="count">Non-negative count of leading eager images.</param>
        /// <returns>Updated options.</returns>
        public ImagesOptions WithEagerFirstImages(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            return options with { EagerFirstCount = count };
        }

        /// <summary>Turns off intrinsic <c>width</c>/<c>height</c> header inspection.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImagesOptions WithoutDimensions() =>
            options with { InjectDimensions = false };

        /// <summary>Turns off automatic <c>loading="lazy"</c> attribute injection.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImagesOptions WithoutLazyLoading() =>
            options with { LazyLoading = false };

        /// <summary>Turns off automatic <c>decoding="async"</c> attribute injection.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImagesOptions WithoutAsyncDecoding() =>
            options with { AsyncDecoding = false };

        /// <summary>Turns off wrapping titled images in <c>&lt;figure&gt;</c> and <c>&lt;figcaption&gt;</c>.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImagesOptions WithoutFigureWrapping() =>
            options with { WrapTitledInFigure = false };

        /// <summary>Disables the images plugin.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImagesOptions Disable() =>
            options with { Enabled = false };
    }
}
