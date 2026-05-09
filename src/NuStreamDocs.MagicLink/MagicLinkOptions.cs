// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.MagicLink;

/// <summary>
/// Configuration for <see cref="MagicLinkPlugin"/>. URL autolinking is always on; <c>#NNN</c> and
/// <c>@user</c> shortrefs are opt-in via <see cref="DefaultRepo"/> /
/// <see cref="ExpandUserMentions"/>.
/// </summary>
public sealed record MagicLinkOptions
{
    /// <summary>
    /// Gets the default <c>org/repo</c> bytes for expanding bare <c>#NNN</c> shortrefs (e.g.
    /// <c>"reactiveui/ReactiveUI"u8</c>). Empty disables <c>#NNN</c> expansion.
    /// </summary>
    public byte[] DefaultRepo { get; init; } = [];

    /// <summary>Gets a value indicating whether <c>@user</c> mentions are rewritten to <c>https://github.com/{user}</c> Markdown links.</summary>
    public bool ExpandUserMentions { get; init; }
}
