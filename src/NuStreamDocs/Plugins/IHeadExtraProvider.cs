// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Optional companion contract for plugins that contribute markup
/// inside every rendered page's <c>&lt;head&gt;</c>.
/// </summary>
/// <remarks>
/// The canonical use case is icon-font / preconnect / meta-tag
/// plugins (Font Awesome, Material Icons, OpenGraph, etc.) that ship
/// in their own assemblies. Theme plugins discover providers during
/// <see cref="IBuildConfigurePlugin.ConfigureAsync"/> by walking
/// <see cref="BuildConfigureContext.Plugins"/>, ask each one to
/// write its head fragment into a shared UTF-8 buffer, and pass the
/// concatenated bytes to the page template's <c>head_extras</c> slot.
/// <para>
/// Implementations stay AOT-clean: write directly to the supplied
/// <see cref="IBufferWriter{T}"/> without materializing strings.
/// </para>
/// </remarks>
public interface IHeadExtraProvider
{
    /// <summary>Writes this provider's head-extras HTML to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink the theme plugin pre-allocates.</param>
    void WriteHeadExtra(IBufferWriter<byte> writer);
}
