// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy;

/// <summary>Builder-extension surface for the privacy plugin.</summary>
public static class DocBuilderPrivacyExtensions
{
    /// <summary>Registers <see cref="PrivacyPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UsePrivacy(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new PrivacyPlugin());
    }

    /// <summary>Registers <see cref="PrivacyPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="PrivacyOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UsePrivacy(this DocBuilder builder, Func<PrivacyOptions, PrivacyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(PrivacyOptions.Default);
        return builder.UsePlugin(new PrivacyPlugin(options));
    }

    /// <summary>Registers <see cref="PrivacyPlugin"/> with caller-tweaked options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="PrivacyOptions.Default"/> and returns the customized set.</param>
    /// <param name="logger">Logger to receive privacy diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UsePrivacy(this DocBuilder builder, Func<PrivacyOptions, PrivacyOptions> configure, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(logger);
        var options = configure(PrivacyOptions.Default);
        return builder.UsePlugin(new PrivacyPlugin(options, logger));
    }

    /// <summary>Compatibility-string adapter over <see cref="PrivacyPlugin.AuditedUrls"/>.</summary>
    /// <param name="plugin">Plugin instance.</param>
    /// <returns>Right-sized URL array wrapped as <see cref="ApiCompatString"/> (implicitly convertible to <c>string</c>).</returns>
    /// <remarks>For consumers that genuinely want UTF-16 strings (logging, assertions, JSON serialization that doesn't cross the <see cref="System.Text.Json.Utf8JsonWriter"/> byte path).</remarks>
    public static ApiCompatString[] AuditedUrlsAsStrings(this PrivacyPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var decoded = Utf8Snapshot.Decode(plugin.AuditedUrls);
        var wrapped = new ApiCompatString[decoded.Length];
        for (var i = 0; i < decoded.Length; i++)
        {
            wrapped[i] = decoded[i];
        }

        return wrapped;
    }
}
