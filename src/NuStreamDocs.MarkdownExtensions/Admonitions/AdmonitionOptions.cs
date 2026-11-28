// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.MarkdownExtensions.Admonitions;

/// <summary>Configuration options for <see cref="AdmonitionPlugin"/>.</summary>
[System.Diagnostics.DebuggerDisplay("AdmonitionOptions: GitHubAlerts={GitHubAlerts}")]
public sealed record AdmonitionOptions
{
    /// <summary>Gets the shared default admonition options.</summary>
    public static AdmonitionOptions Default { get; } = new();

    /// <summary>Gets a value indicating whether GitHub blockquote alerts (<c>&gt; [!NOTE]</c>, <c>&gt; [!WARNING]</c>, etc.) render as admonitions.</summary>
    public bool GitHubAlerts { get; init; } = true;
}
