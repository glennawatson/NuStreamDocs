// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>
/// <see cref="IIconResolver"/> implementation. The default constructor
/// routes lookups through the generated <see cref="MdiIconBundle"/>
/// catalogue (full upstream MDI set, baked into the assembly's
/// <c>#Blob</c> heap as <c>"..."u8</c> literals — zero startup cost).
/// The <see cref="MdiIconLookup"/> overload uses a custom user-supplied
/// catalogue, which is the path tests + niche consumers take when they
/// want a smaller or augmented bundle.
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

    /// <inheritdoc/>
    public bool TryResolve(ReadOnlySpan<byte> iconName, out ReadOnlySpan<byte> svg) =>
        _customLookup is not null
            ? _customLookup.TryGet(iconName, out svg)
            : MdiIconBundle.TryGet(iconName, out svg);
}
