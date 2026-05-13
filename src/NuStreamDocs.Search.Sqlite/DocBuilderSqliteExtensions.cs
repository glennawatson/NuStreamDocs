// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>Builder-extension surface for <see cref="SqliteSearchPlugin"/>.</summary>
public static class DocBuilderSqliteExtensions
{
    /// <summary>Registers <see cref="SqliteSearchPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSqliteSearch(this DocBuilder builder) => builder.UsePlugin(new SqliteSearchPlugin());

    /// <summary>Registers <see cref="SqliteSearchPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="SqliteOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSqliteSearch(this DocBuilder builder, Func<SqliteOptions, SqliteOptions> configure)
    {
        var options = configure(SqliteOptions.Default);
        return builder.UsePlugin(new SqliteSearchPlugin(options));
    }

    /// <summary>Registers <see cref="SqliteSearchPlugin"/> with caller-supplied options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSqliteSearch(this DocBuilder builder, in SqliteOptions options, ILogger logger) =>
        builder.UsePlugin(new SqliteSearchPlugin(options, logger));
}
