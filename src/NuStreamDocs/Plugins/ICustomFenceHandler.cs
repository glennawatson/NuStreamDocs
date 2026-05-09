// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin contract that contributes a fence handler to the superfences dispatcher; claims a
/// fenced-code language and renders it as bespoke HTML instead of the default
/// <c>&lt;pre&gt;&lt;code&gt;</c> block.
/// </summary>
public interface ICustomFenceHandler
{
    /// <summary>Gets the UTF-8 language identifier this handler claims (e.g. <c>"mermaid"u8</c>, <c>"math"u8</c>).</summary>
    ReadOnlySpan<byte> Language { get; }

    /// <summary>Renders <paramref name="content"/> into <paramref name="writer"/>.</summary>
    /// <param name="content">UTF-8 fence body, with HTML entities decoded back to their literal bytes.</param>
    /// <param name="writer">UTF-8 sink that replaces the source <c>&lt;pre&gt;&lt;code&gt;</c> block.</param>
    void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer);
}
