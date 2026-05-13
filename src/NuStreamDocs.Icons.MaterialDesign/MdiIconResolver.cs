// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>Resolves Material Design Icon names to inline 24×24 SVG markup.</summary>
public sealed class MdiIconResolver : IIconResolver
{
    /// <summary>Optional user-supplied lookup; <c>null</c> means route through the generated catalogue.</summary>
    private readonly MdiIconLookup? _customLookup;

    /// <summary>Initializes a new instance of the <see cref="MdiIconResolver"/> class backed by the generated MDI catalogue.</summary>
    public MdiIconResolver()
        : this(null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MdiIconResolver"/> class backed by <paramref name="customLookup"/>.</summary>
    /// <param name="customLookup">User-supplied lookup (e.g. for tests or augmented bundles).</param>
    public MdiIconResolver(MdiIconLookup? customLookup) => _customLookup = customLookup;

    /// <summary>Gets the shared SVG opening bytes.</summary>
    private static ReadOnlySpan<byte> SvgPrefix =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\""u8;

    /// <summary>Gets the shared SVG closing bytes.</summary>
    private static ReadOnlySpan<byte> SvgSuffix => "\"/></svg>"u8;

    /// <inheritdoc/>
    public bool TryResolve(ReadOnlySpan<byte> iconName, IBufferWriter<byte> writer)
    {
        if (!TryGetPath(iconName, out var path))
        {
            return false;
        }

        writer.Write(SvgPrefix);
        writer.Write(path);
        writer.Write(SvgSuffix);
        return true;
    }

    /// <summary>Resolves the icon name to its SVG path-data bytes.</summary>
    /// <param name="iconName">UTF-8 icon name bytes.</param>
    /// <param name="path">UTF-8 path-data bytes on hit.</param>
    /// <returns>True when the icon is in the active catalogue.</returns>
    private bool TryGetPath(ReadOnlySpan<byte> iconName, out ReadOnlySpan<byte> path) =>
        _customLookup is not null
            ? _customLookup.TryGet(iconName, out path)
            : MdiIconBundle.TryGet(iconName, out path);
}
