// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.MagicLink;

/// <summary>Configuration for <see cref="MagicLinkPlugin"/>.</summary>
/// <remarks>
/// URL autolinking (bare <c>http://</c>, <c>https://</c>, <c>ftp://</c>,
/// <c>ftps://</c>, <c>mailto:</c>) is always on. GitHub-style shortref
/// expansion (<c>#NNN</c> issue / pull-request references and
/// <c>@user</c> mentions) is opt-in via <see cref="DefaultRepo"/>:
/// configure <c>org/repo</c> bytes and the rewriter expands shortrefs
/// found at word boundaries into Markdown links.
/// </remarks>
public sealed record MagicLinkOptions
{
    /// <summary>Gets the default <c>org/repo</c> bytes used when expanding bare <c>#NNN</c> shortrefs (e.g. <c>"reactiveui/ReactiveUI"u8</c>); empty disables <c>#NNN</c> expansion.</summary>
    public byte[] DefaultRepo { get; init; } = [];

    /// <summary>Gets a value indicating whether <c>@user</c> mentions are rewritten to <c>https://github.com/{user}</c> Markdown links.</summary>
    public bool ExpandUserMentions { get; init; }
}
