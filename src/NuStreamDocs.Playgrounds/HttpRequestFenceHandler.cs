// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Playgrounds;

/// <summary><see cref="ICustomFenceHandler"/> claiming <c>http-request</c> fences when <c>SuperFencesPlugin</c> is registered.</summary>
[System.Diagnostics.DebuggerDisplay("HttpRequestFenceHandler: {Name}")]
public sealed class HttpRequestFenceHandler : IPlugin, ICustomFenceHandler
{
    /// <summary>Configured options.</summary>
    private readonly PlaygroundsOptions _options;

    /// <summary>Initializes a new instance of the <see cref="HttpRequestFenceHandler"/> class.</summary>
    /// <param name="options">Playground options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public HttpRequestFenceHandler(PlaygroundsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "playgrounds-http-fence"u8;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Language => "http-request"u8;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer) =>
        HttpRequestWidgetEmitter.Render(content, _options.EnableInteractiveTryIt, writer);
}
