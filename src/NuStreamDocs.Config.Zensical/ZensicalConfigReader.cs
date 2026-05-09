// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Config.MkDocs;

namespace NuStreamDocs.Config.Zensical;

/// <summary>
/// <see cref="IConfigReader"/> implementation for Zensical-flavored <c>zensical.toml</c> files.
/// </summary>
public sealed class ZensicalConfigReader : IConfigReader
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> FormatName => "zensical"u8;

    /// <inheritdoc/>
    public bool RecognizesExtension(ReadOnlySpan<char> extension) =>
        extension.Equals(".toml", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public MkDocsConfig Read(ReadOnlySpan<byte> utf8Source) =>
        ConfigReaderJsonPipeline.Read(utf8Source, TomlToJson.Convert);

    /// <inheritdoc/>
    public Task<MkDocsConfig> ReadAsync(Stream utf8Stream, CancellationToken cancellationToken) =>
        ConfigReaderJsonPipeline.ReadAsync(utf8Stream, TomlToJson.ConvertAsync, cancellationToken);
}
