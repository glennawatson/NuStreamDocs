// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Playgrounds;

/// <summary><see cref="ICustomFenceHandler"/> claiming <c>playground</c> fences when <c>SuperFencesPlugin</c> is registered.</summary>
[System.Diagnostics.DebuggerDisplay("PlaygroundFenceHandler: {Name}")]
public sealed class PlaygroundFenceHandler : IPlugin, ICustomFenceHandler
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "playgrounds-sandbox-fence"u8;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Language => "playground"u8;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer) =>
        CodeSandboxWidgetEmitter.Render(content, writer);
}
