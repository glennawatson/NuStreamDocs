// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Config.MkDocs;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader;

/// <summary>
/// Reads a local structured-data file — JSON or YAML — and turns each object in a collection into a
/// Markdown page via a <see cref="ContentMapping"/>. A JSON document may have the array at its root or
/// under a key (set via the mapping's collection pointer); a YAML document must place the array under
/// a key, since YAML files in this project are mapping-rooted like <c>mkdocs.yml</c>.
/// </summary>
public sealed class FileContentLoader : IContentLoader
{
    /// <summary>Path to the source file; absolute, or relative to the build input root.</summary>
    private readonly FilePath _path;

    /// <summary>Field mapping.</summary>
    private readonly ContentMapping _mapping;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="FileContentLoader"/> class.</summary>
    /// <param name="path">Source file path (absolute, or relative to the input root). Extension <c>.yml</c>/<c>.yaml</c> is parsed as YAML; anything else as JSON.</param>
    /// <param name="mapping">Field mapping.</param>
    public FileContentLoader(FilePath path, ContentMapping mapping)
        : this(path, mapping, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FileContentLoader"/> class.</summary>
    /// <param name="path">Source file path (absolute, or relative to the input root).</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public FileContentLoader(FilePath path, ContentMapping mapping, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(path.Value);
        ArgumentNullException.ThrowIfNull(mapping);
        mapping.Validate();
        _path = path;
        _mapping = mapping;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "file"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        var resolved = ResolvePath(context.InputRoot);
        if (!File.Exists(resolved.Value))
        {
            throw new ContentLoaderException(StringCompose.Concat("Content source file not found: ", resolved.Value));
        }

        var bytes = await File.ReadAllBytesAsync(resolved.Value, cancellationToken).ConfigureAwait(false);
        var json = IsYaml(_path) ? ConvertYamlToJson(bytes) : bytes;
        return JsonContentMapper.Map(json, _mapping, Name, _logger);
    }

    /// <summary>True when the file should be parsed as YAML.</summary>
    /// <param name="path">Source file path.</param>
    /// <returns><see langword="true"/> for <c>.yml</c> / <c>.yaml</c>.</returns>
    private static bool IsYaml(FilePath path) =>
        path.Value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
        || path.Value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

    /// <summary>Converts a YAML document to its JSON byte equivalent.</summary>
    /// <param name="yaml">UTF-8 YAML bytes.</param>
    /// <returns>UTF-8 JSON bytes.</returns>
    private static byte[] ConvertYamlToJson(byte[] yaml)
    {
        ArrayBufferWriter<byte> buffer = new(yaml.Length);
        using (Utf8JsonWriter writer = new(buffer))
        {
            YamlToJson.Convert(yaml, writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Resolves the source path against the input root when it is relative.</summary>
    /// <param name="inputRoot">Build input root.</param>
    /// <returns>An absolute file path.</returns>
    private FilePath ResolvePath(DirectoryPath inputRoot)
    {
        if (Path.IsPathRooted(_path.Value) || inputRoot.IsEmpty)
        {
            return _path;
        }

        return inputRoot.File(_path.Value);
    }
}
