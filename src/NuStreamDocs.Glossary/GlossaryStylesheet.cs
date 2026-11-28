// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Glossary;

/// <summary>Accessible zero-JavaScript stylesheet for <c>.nd-glossary-term</c> hover and keyboard-focus tooltips.</summary>
internal static class GlossaryStylesheet
{
    /// <summary>Gets the UTF-8 CSS payload written to <c>assets/stylesheets/nstd-glossary.css</c>.</summary>
    internal static ReadOnlySpan<byte> Bytes => """
        .nd-glossary-term {
          position: relative;
          text-decoration: underline dotted currentColor;
          text-underline-offset: 0.18em;
          cursor: help;
        }
        .nd-glossary-term:focus-visible {
          outline: 2px solid currentColor;
          outline-offset: 2px;
          border-radius: 2px;
        }
        .nd-glossary-term:hover::after,
        .nd-glossary-term:focus-visible::after {
          content: attr(title);
          position: absolute;
          left: 0;
          bottom: calc(100% + 0.35rem);
          z-index: 40;
          min-width: 14rem;
          max-width: 22rem;
          padding: 0.45rem 0.65rem;
          border-radius: 0.35rem;
          background: #1f2937;
          color: #f9fafb;
          font-size: 0.8rem;
          line-height: 1.35;
          white-space: normal;
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.22);
          pointer-events: none;
        }
        """u8;
}
