// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Optimise;

/// <summary>Configuration for <see cref="HtmlMinifyPlugin"/>.</summary>
/// <param name="StripComments">Remove HTML comments (<c>&lt;!-- … --&gt;</c>); IE conditional comments are dropped along with the rest.</param>
/// <param name="CollapseWhitespace">Collapse runs of inter-tag whitespace into a single space (or strip entirely between block-level tags).</param>
public sealed record HtmlMinifyOptions(bool StripComments, bool CollapseWhitespace)
{
    /// <summary>Gets the default option set — strip comments and collapse whitespace.</summary>
    public static HtmlMinifyOptions Default { get; } = new(StripComments: true, CollapseWhitespace: true);
}
