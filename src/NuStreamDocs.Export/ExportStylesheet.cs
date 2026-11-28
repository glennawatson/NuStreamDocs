// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Export;

/// <summary>Print and PDF layout rules embedded directly into the standalone export bundle.</summary>
internal static class ExportStylesheet
{
    /// <summary>Gets the UTF-8 stylesheet embedded in <c>&lt;style&gt;</c> inside <c>/print.html</c>.</summary>
    internal static ReadOnlySpan<byte> Bytes => """
        @page { margin: 18mm 16mm; }
        body {
          font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
          line-height: 1.6;
          color: #111827;
          max-width: 54rem;
          margin: 0 auto;
          padding: 2rem 1.25rem;
        }
        .nd-export-cover {
          border-bottom: 2px solid #e5e7eb;
          padding-bottom: 1.5rem;
          margin-bottom: 2rem;
        }
        .nd-export-subtitle { color: #4b5563; font-size: 1.1rem; margin-top: 0.35rem; }
        .nd-export-toc {
          background: #f9fafb;
          border: 1px solid #e5e7eb;
          border-radius: 0.5rem;
          padding: 1rem 1.5rem;
          margin-bottom: 2.5rem;
        }
        .nd-export-chapter {
          padding-top: 1.5rem;
          margin-top: 2rem;
          border-top: 1px solid #e5e7eb;
        }
        pre, table, figure, blockquote { break-inside: avoid; }
        pre { background: #f3f4f6; padding: 0.85rem; border-radius: 0.35rem; overflow-x: auto; }
        table { border-collapse: collapse; width: 100%; margin: 1rem 0; }
        th, td { border: 1px solid #d1d5db; padding: 0.45rem 0.65rem; text-align: left; }
        @media print {
          body { max-width: none; padding: 0; }
          .nd-export-chapter { break-before: page; border-top: 0; margin-top: 0; padding-top: 0; }
          .headerlink, .md-clipboard, .nd-http-tryit { display: none !important; }
        }
        """u8;
}
