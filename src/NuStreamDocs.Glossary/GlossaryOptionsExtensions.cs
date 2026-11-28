// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Glossary;

/// <summary>Fluent builder helpers for <see cref="GlossaryOptions"/>.</summary>
public static class GlossaryOptionsExtensions
{
    /// <summary>Extension members for <see cref="GlossaryOptions"/>.</summary>
    /// <param name="options">Source options record.</param>
    extension(GlossaryOptions options)
    {
        /// <summary>Returns a copy reading glossary definitions from <paramref name="glossaryFile"/>.</summary>
        /// <param name="glossaryFile">YAML or JSON glossary path.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        public GlossaryOptions WithGlossaryFile(FilePath glossaryFile)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options with { GlossaryFile = glossaryFile };
        }

        /// <summary>Returns a copy emitting the glossary page under <paramref name="subdirectory"/>.</summary>
        /// <param name="subdirectory">Relative subdirectory segment.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        public GlossaryOptions WithOutputSubdirectory(PathSegment subdirectory)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options with { OutputSubdirectory = subdirectory };
        }

        /// <summary>Returns a copy with <paramref name="title"/> as the UTF-8 glossary page heading.</summary>
        /// <param name="title">Non-empty UTF-8 page title.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is empty.</exception>
        public GlossaryOptions WithPageTitle(ReadOnlySpan<byte> title)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (title.IsEmpty)
            {
                throw new ArgumentException("Glossary page title must be non-empty.", nameof(title));
            }

            return options with { PageTitle = title.ToArray() };
        }

        /// <summary>Appends a programmatic glossary term and definition.</summary>
        /// <param name="term">Non-empty UTF-8 term.</param>
        /// <param name="definition">Non-empty UTF-8 definition.</param>
        /// <returns>Updated options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="term"/> or <paramref name="definition"/> is empty.</exception>
        public GlossaryOptions AddTerm(ReadOnlySpan<byte> term, ReadOnlySpan<byte> definition)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (term.IsEmpty)
            {
                throw new ArgumentException("Glossary term must be non-empty.", nameof(term));
            }

            if (definition.IsEmpty)
            {
                throw new ArgumentException("Glossary definition must be non-empty.", nameof(definition));
            }

            GlossaryEntry entry = new(term.ToArray(), definition.ToArray(), GlossaryLoader.Slugify(term));
            return options with { Terms = [.. options.Terms, entry] };
        }
    }
}
