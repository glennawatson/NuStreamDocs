// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Images;

/// <summary>Default stylesheet for <c>&lt;figure class="nd-figure"&gt;</c> and <c>&lt;figcaption class="nd-figcaption"&gt;</c> emitted by <see cref="ImagesPlugin"/>.</summary>
public static class ImagesStylesheet
{
    /// <summary>Gets the site-relative asset path for the stylesheet.</summary>
    public static FilePath AssetPath => new("assets/stylesheets/images.css");

    /// <summary>Gets the stylesheet rules as UTF-8 bytes.</summary>
    private static ReadOnlySpan<byte> CssBytes =>
        """
        .nd-figure {
          margin: 1.25rem 0;
          text-align: center;
        }

        .nd-figure img {
          max-width: 100%;
          height: auto;
        }

        .nd-figcaption {
          margin-top: 0.45rem;
          font-size: 0.85rem;
          color: #57606a;
        }

        [data-md-color-scheme="slate"] .nd-figcaption {
          color: #8b949e;
        }
        """u8;

    /// <summary>Returns a copy of the stylesheet bytes.</summary>
    /// <returns>UTF-8 CSS bytes.</returns>
    public static byte[] GetBytes() => [.. CssBytes];
}
