// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Audit;

/// <summary>Builder extensions that register <see cref="AuditPlugin"/>.</summary>
public static class DocBuilderAuditExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="AuditPlugin"/> with default options (warn-only, every lint enabled).</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAudit() =>
            builder.UsePlugin(new AuditPlugin());

        /// <summary>Registers <see cref="AuditPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAudit(AuditOptions options) =>
            builder.UsePlugin(new AuditPlugin(options));

        /// <summary>Registers <see cref="AuditPlugin"/> with options derived from the default set.</summary>
        /// <param name="configure">Transforms <see cref="AuditOptions.Default"/> into the options to use.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseAudit(Func<AuditOptions, AuditOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new AuditPlugin(configure(AuditOptions.Default)));
        }

        /// <summary>Registers <see cref="AuditPlugin"/> with the supplied options and logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger that receives audit findings.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAudit(AuditOptions options, ILogger logger) =>
            builder.UsePlugin(new AuditPlugin(options, logger));
    }
}
