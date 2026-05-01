// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>
/// <see cref="IIconResolver"/> implementation backed by <see cref="MdiIconLookup"/>.
/// Wraps each resolved SVG path in the standard MDI 24×24 viewBox so
/// callers can inline the result directly into rendered HTML — same
/// shape mkdocs-material emits for its <c>:material-foo:</c> shortcodes.
/// </summary>
public sealed class MdiIconResolver : IIconResolver
{
    /// <summary>Resolved icon-data lookup.</summary>
    private readonly MdiIconLookup _lookup;

    /// <summary>Initializes a new instance of the <see cref="MdiIconResolver"/> class with the default MDI bundle.</summary>
    public MdiIconResolver()
        : this(MdiIconBundle.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MdiIconResolver"/> class with a caller-supplied lookup.</summary>
    /// <param name="lookup">Icon lookup table.</param>
    public MdiIconResolver(MdiIconLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        _lookup = lookup;
    }

    /// <inheritdoc/>
    public bool TryResolve(ReadOnlySpan<byte> iconName, out ReadOnlySpan<byte> svg) =>
        _lookup.TryGet(iconName, out svg);
}
