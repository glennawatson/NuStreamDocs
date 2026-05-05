// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Registers byte sequences that signal a page has cross-page work pending.
/// </summary>
/// <remarks>
/// The build engine consults this registry after rendering each page: if
/// none of the registered markers appear in the emitted HTML, the page
/// skips the cross-page barrier and writes immediately. Plugins that
/// participate in <see cref="IBuildResolvePlugin"/> /
/// <see cref="IPagePostResolvePlugin"/> register their distinguishing
/// marker bytes during <see cref="IBuildConfigurePlugin.ConfigureAsync"/>
/// (e.g. <c>"@autoref:"u8</c>) so the engine can skip the in-memory
/// page-buffer hold for pages that contain no such markers.
/// </remarks>
public sealed class CrossPageMarkerRegistry
{
    /// <summary>Initial slot capacity for registered markers.</summary>
    private const int InitialCapacity = 4;

    /// <summary>Backing list of registered marker byte sequences.</summary>
    private readonly List<byte[]> _markers = new(InitialCapacity);

    /// <summary>Gets the registered marker byte sequences.</summary>
    public IReadOnlyList<byte[]> Markers => _markers;

    /// <summary>Registers a marker byte sequence whose presence indicates the page needs cross-page resolution.</summary>
    /// <param name="needle">UTF-8 byte sequence identifying the cross-page marker (e.g. <c>[.. "@autoref:"u8]</c>).</param>
    public void Register(byte[] needle)
    {
        ArgumentNullException.ThrowIfNull(needle);
        if (needle.Length is 0)
        {
            throw new ArgumentException("Marker needle must be non-empty.", nameof(needle));
        }

        _markers.Add(needle);
    }
}
