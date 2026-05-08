// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Common;

/// <summary>A footer social link emitted as an <c>&lt;a class="md-social__link"&gt;</c> with an inline SVG icon.</summary>
/// <param name="Url">UTF-8 destination URL bytes.</param>
/// <param name="Title">UTF-8 link title / tooltip bytes.</param>
/// <param name="IconSvg">UTF-8 raw SVG markup bytes emitted verbatim inside the anchor.</param>
public readonly record struct ThemeSocialLink(byte[] Url, byte[] Title, byte[] IconSvg);
