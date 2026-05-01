// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>
/// <see cref="IIconResolver"/> implementation backed by the generated
/// MDI catalogue. Each lookup hit writes a 24×24 inline SVG wrapper
/// plus the per-icon path data — the wrapper bytes live as one
/// <c>"..."u8</c> literal, so we don't pay the per-icon storage cost
/// the deduplicated catalogue eliminated.
/// </summary>
public sealed class MdiIconResolver : IIconResolver
{
    /// <summary>Optional user-supplied lookup; <c>null</c> means route through the generated <see cref="MdiIconBundle"/>.</summary>
    private readonly MdiIconLookup? _customLookup;

    /// <summary>Initializes a new instance of the <see cref="MdiIconResolver"/> class backed by the generated MDI catalogue.</summary>
    public MdiIconResolver()
        : this(null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MdiIconResolver"/> class backed by <paramref name="customLookup"/>.</summary>
    /// <param name="customLookup">User-supplied lookup (e.g. for tests or augmented bundles).</param>
    public MdiIconResolver(MdiIconLookup? customLookup) => _customLookup = customLookup;

    /// <summary>Gets the shared SVG opening — written once per icon hit. Includes <c>aria-hidden</c> so screen readers ignore the glyph (mkdocs-material convention).</summary>
    private static ReadOnlySpan<byte> SvgPrefix => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\""u8;

    /// <summary>Gets the shared SVG closing — written once per icon hit.</summary>
    private static ReadOnlySpan<byte> SvgSuffix => "\"/></svg>"u8;

    /// <inheritdoc/>
    public bool TryResolve(ReadOnlySpan<byte> iconName, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (!TryGetPath(iconName, out var path))
        {
            return false;
        }

        writer.Write(SvgPrefix);
        writer.Write(path);
        writer.Write(SvgSuffix);
        return true;
    }

    /// <summary>Resolves <paramref name="iconName"/> to the underlying SVG path data (the value of the <c>&lt;path d="…"/&gt;</c> attribute).</summary>
    /// <param name="iconName">UTF-8 icon name bytes.</param>
    /// <param name="path">UTF-8 path-data bytes on hit.</param>
    /// <returns>True when the icon is in the active catalogue.</returns>
    private bool TryGetPath(ReadOnlySpan<byte> iconName, out ReadOnlySpan<byte> path) =>
        _customLookup is not null
            ? _customLookup.TryGet(iconName, out path)
            : MdiIconBundle.TryGet(iconName, out path);
}
