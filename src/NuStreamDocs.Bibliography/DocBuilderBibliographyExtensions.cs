// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Bibliography.Styles;
using NuStreamDocs.Bibliography.Styles.Aglc4;
using NuStreamDocs.Building;

namespace NuStreamDocs.Bibliography;

/// <summary>Builder-extension surface for <see cref="BibliographyPlugin"/>.</summary>
public static class DocBuilderBibliographyExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="BibliographyPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseBibliography(BibliographyOptions options) =>
            builder.UsePlugin(new BibliographyPlugin(options));

        /// <summary>Registers <see cref="BibliographyPlugin"/> with a fluent <see cref="BibliographyDatabaseBuilder"/>.</summary>
        /// <param name="configureDatabase">Database build callback.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseBibliography(
            Action<BibliographyDatabaseBuilder> configureDatabase)
        {
            BibliographyDatabaseBuilder dbBuilder = new();
            configureDatabase(dbBuilder);
            return builder.UseBibliography(new BibliographyOptions(dbBuilder.Build(), Aglc4Style.Instance, false));
        }

        /// <summary>Registers <see cref="BibliographyPlugin"/> with a fluent database and an explicit style.</summary>
        /// <param name="style">Citation style.</param>
        /// <param name="configureDatabase">Database build callback.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseBibliography(
            ICitationStyle style,
            Action<BibliographyDatabaseBuilder> configureDatabase)
        {
            BibliographyDatabaseBuilder dbBuilder = new();
            configureDatabase(dbBuilder);
            return builder.UseBibliography(new BibliographyOptions(dbBuilder.Build(), style, false));
        }
    }
}
