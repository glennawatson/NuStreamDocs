// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.I18n;

/// <summary>Default stylesheet for the <c>.nd-lang-switcher</c> navigation bar emitted by <see cref="I18nPlugin"/>.</summary>
public static class I18nStylesheet
{
    /// <summary>Gets the site-relative path for the stylesheet.</summary>
    public static FilePath AssetPath => new("assets/stylesheets/i18n.css");

    /// <summary>Gets the stylesheet UTF-8 bytes.</summary>
    private static ReadOnlySpan<byte> CssBytes =>
        """
        .nd-lang-switcher {
          display: flex;
          flex-wrap: wrap;
          gap: 0.5rem;
          align-items: center;
          padding: 0.4rem 1rem;
          font-size: 0.82rem;
          border-bottom: 1px solid rgba(128, 128, 128, 0.2);
        }

        .nd-lang-switcher a {
          text-decoration: none;
          padding: 0.15rem 0.45rem;
          border-radius: 4px;
        }

        .nd-lang-switcher a[aria-current="page"] {
          font-weight: 700;
          text-decoration: underline;
        }
        """u8;

    /// <summary>Returns a copy of the stylesheet bytes.</summary>
    /// <returns>UTF-8 CSS bytes.</returns>
    public static byte[] GetBytes() => [.. CssBytes];
}
