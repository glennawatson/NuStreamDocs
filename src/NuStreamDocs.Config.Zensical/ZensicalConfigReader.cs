// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.Zensical;

/// <summary>
/// <see cref="IConfigReader"/> implementation that reads
/// Zensical-flavoured <c>zensical.toml</c> via the in-house TOML to
/// UTF-8 JSON pipeline.
/// </summary>
/// <remarks>
/// The TOML pipeline produces the same JSON shape
/// <see cref="MkDocsConfigJsonParser"/> understands, so the post-
/// conversion path is shared between formats.
/// </remarks>
public sealed class ZensicalConfigReader : IConfigReader
{
    /// <inheritdoc/>
    public string FormatName => "zensical";

    /// <inheritdoc/>
    public bool RecognisesExtension(ReadOnlySpan<char> extension) =>
        extension.Equals(".toml", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public MkDocsConfig Read(ReadOnlySpan<byte> utf8Source) =>
        ConfigReaderJsonPipeline.Read(utf8Source, TomlToJson.Convert);

    /// <inheritdoc/>
    public Task<MkDocsConfig> ReadAsync(Stream utf8Stream, CancellationToken cancellationToken) =>
        ConfigReaderJsonPipeline.ReadAsync(utf8Stream, TomlToJson.ConvertAsync, cancellationToken);
}
