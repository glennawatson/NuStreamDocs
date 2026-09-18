// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>Builder-extension surface for <see cref="SqliteSearchPlugin"/>.</summary>
public static class DocBuilderSqliteExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">The builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="SqliteSearchPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSqliteSearch() => builder.UsePlugin(new SqliteSearchPlugin());

        /// <summary>Registers <see cref="SqliteSearchPlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="SqliteOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseSqliteSearch(Func<SqliteOptions, SqliteOptions> configure)
        {
            var options = configure(SqliteOptions.Default);
            return builder.UsePlugin(new SqliteSearchPlugin(options));
        }

        /// <summary>Registers <see cref="SqliteSearchPlugin"/> with caller-supplied options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSqliteSearch(in SqliteOptions options, ILogger logger) =>
            builder.UsePlugin(new SqliteSearchPlugin(options, logger));
    }
}
