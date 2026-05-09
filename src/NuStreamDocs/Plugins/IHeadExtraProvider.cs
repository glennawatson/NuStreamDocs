// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Optional contract for plugins contributing markup inside every rendered page's
/// <c>&lt;head&gt;</c> (icon fonts, preconnect tags, meta tags). Theme plugins discover providers
/// during configure and concatenate their fragments into the page template's <c>head_extras</c>
/// slot.
/// </summary>
public interface IHeadExtraProvider
{
    /// <summary>Writes this provider's head-extras HTML to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink the theme plugin pre-allocates.</param>
    void WriteHeadExtra(IBufferWriter<byte> writer);
}
