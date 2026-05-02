// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Plugins;
using NuStreamDocs.Versions.Logging;

namespace NuStreamDocs.Versions;

/// <summary>
/// Plugin that publishes a mike-compatible <c>versions.json</c> manifest
/// in the parent directory of the build's output root.
/// </summary>
/// <remarks>
/// The user is expected to point the build at a version-specific
/// subdirectory (e.g. <c>builder.WithOutput("./site/0.4.2")</c>); the
/// plugin then upserts the version's entry into <c>./site/versions.json</c>
/// during <see cref="OnFinalizeAsync"/>. This keeps each version's content
/// isolated and lets a deploy script swap the parent symlink atomically.
/// </remarks>
public sealed class VersionsPlugin(VersionOptions options, ILogger logger) : IDocPlugin
{
    /// <summary>Configured options.</summary>
    private readonly VersionOptions _options = ValidateOptions(options);

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Initializes a new instance of the <see cref="VersionsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public VersionsPlugin(VersionOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <inheritdoc/>
    public string Name => "versions";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var parentDir = Path.GetDirectoryName(context.OutputRoot.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(parentDir))
        {
            // Output already at filesystem root — nowhere to write a parent manifest.
            return ValueTask.CompletedTask;
        }

        var manifestPath = Path.Combine(parentDir, VersionsManifest.FileName);
        var existing = VersionsManifest.Read(parentDir);
        VersionsLoggingHelper.LogManifestRead(_logger, manifestPath, existing.Length);
        var merged = VersionsManifest.Upsert(existing, _options.ToEntry());
        VersionsManifest.Write(parentDir, merged);
        VersionsLoggingHelper.LogManifestWrite(_logger, manifestPath, merged.Length);

        return ValueTask.CompletedTask;
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static VersionOptions ValidateOptions(VersionOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
    }
}
