// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// <see cref="IConfigReader"/> implementation that reads
/// <c>mkdocs.yml</c> through <see cref="YamlToJson"/> +
/// <see cref="MkDocsConfigJsonParser"/>.
/// </summary>
public sealed class MkDocsConfigReader : IConfigReader
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> FormatName => "mkdocs"u8;

    /// <inheritdoc/>
    public bool RecognizesExtension(ReadOnlySpan<char> extension) =>
        extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public MkDocsConfig Read(ReadOnlySpan<byte> utf8Source) =>
        ConfigReaderJsonPipeline.Read(utf8Source, YamlToJson.Convert);

    /// <inheritdoc/>
    public Task<MkDocsConfig> ReadAsync(Stream utf8Stream, CancellationToken cancellationToken) =>
        ConfigReaderJsonPipeline.ReadAsync(utf8Stream, YamlToJson.ConvertAsync, cancellationToken);
}
