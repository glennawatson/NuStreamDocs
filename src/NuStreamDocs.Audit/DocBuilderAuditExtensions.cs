// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Audit;

/// <summary>Builder extensions that register <see cref="AuditPlugin"/>.</summary>
public static class DocBuilderAuditExtensions
{
    /// <summary>Registers <see cref="AuditPlugin"/> with default options (warn-only, every lint enabled).</summary>
    /// <param name="builder">Doc builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseAudit(this DocBuilder builder) =>
        builder.UsePlugin(new AuditPlugin());

    /// <summary>Registers <see cref="AuditPlugin"/> with the supplied options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseAudit(this DocBuilder builder, AuditOptions options) =>
        builder.UsePlugin(new AuditPlugin(options));

    /// <summary>Registers <see cref="AuditPlugin"/> with options derived from the default set.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="configure">Transforms <see cref="AuditOptions.Default"/> into the options to use.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseAudit(this DocBuilder builder, Func<AuditOptions, AuditOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.UsePlugin(new AuditPlugin(configure(AuditOptions.Default)));
    }

    /// <summary>Registers <see cref="AuditPlugin"/> with the supplied options and logger.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger that receives audit findings.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseAudit(this DocBuilder builder, AuditOptions options, ILogger logger) =>
        builder.UsePlugin(new AuditPlugin(options, logger));
}
