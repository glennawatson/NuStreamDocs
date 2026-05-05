// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Styles;
using NuStreamDocs.Bibliography.Styles.Aglc4;
using NuStreamDocs.Building;

namespace NuStreamDocs.Bibliography;

/// <summary>Builder-extension surface for <see cref="BibliographyPlugin"/>.</summary>
public static class DocBuilderBibliographyExtensions
{
    /// <summary>Registers <see cref="BibliographyPlugin"/> with the supplied options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseBibliography(this DocBuilder builder, BibliographyOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new BibliographyPlugin(options));
    }

    /// <summary>Registers <see cref="BibliographyPlugin"/> with a fluent <see cref="BibliographyDatabaseBuilder"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configureDatabase">Database build callback.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseBibliography(this DocBuilder builder, Action<BibliographyDatabaseBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureDatabase);
        BibliographyDatabaseBuilder dbBuilder = new();
        configureDatabase(dbBuilder);
        return builder.UseBibliography(new BibliographyOptions(dbBuilder.Build(), Aglc4Style.Instance, WarnOnMissing: false));
    }

    /// <summary>Registers <see cref="BibliographyPlugin"/> with a fluent database and an explicit style.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="configureDatabase">Database build callback.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseBibliography(this DocBuilder builder, ICitationStyle style, Action<BibliographyDatabaseBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(configureDatabase);
        BibliographyDatabaseBuilder dbBuilder = new();
        configureDatabase(dbBuilder);
        return builder.UseBibliography(new BibliographyOptions(dbBuilder.Build(), style, WarnOnMissing: false));
    }
}
