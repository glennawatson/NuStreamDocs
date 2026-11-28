// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Export;

/// <summary>Fluent builder helpers for <see cref="ExportOptions"/>.</summary>
public static class ExportOptionsExtensions
{
    /// <summary>Extension members for <see cref="ExportOptions"/>.</summary>
    /// <param name="options">Source options record.</param>
    extension(ExportOptions options)
    {
        /// <summary>Returns a copy writing the consolidated bundle to <paramref name="fileName"/>.</summary>
        /// <param name="fileName">Relative HTML filename under the output root.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="fileName"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is empty or whitespace.</exception>
        public ExportOptions WithOutputFileName(string fileName)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            return options with { OutputFileName = fileName };
        }

        /// <summary>Returns a copy setting the cover heading to <paramref name="title"/>.</summary>
        /// <param name="title">Non-empty UTF-8 cover title.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is empty.</exception>
        public ExportOptions WithDocumentTitle(ReadOnlySpan<byte> title)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (title.IsEmpty)
            {
                throw new ArgumentException("Export document title must be non-empty.", nameof(title));
            }

            return options with { DocumentTitle = title.ToArray() };
        }

        /// <summary>Returns a copy setting the optional cover subtitle to <paramref name="subtitle"/>.</summary>
        /// <param name="subtitle">UTF-8 subtitle bytes (may be empty to clear).</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        public ExportOptions WithSubtitle(ReadOnlySpan<byte> subtitle)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options with { Subtitle = subtitle.ToArray() };
        }

        /// <summary>Returns a copy toggling the consolidated table of contents.</summary>
        /// <param name="include"><c>true</c> to render the chapter table of contents; otherwise, <c>false</c>.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        public ExportOptions WithTableOfContents(bool include)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options with { IncludeTableOfContents = include };
        }
    }
}
