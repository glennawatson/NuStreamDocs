// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Llms;

/// <summary>Configuration for <see cref="LlmsPlugin"/>.</summary>
/// <param name="EmitFullText">When <c>true</c>, writes <c>llms-full.txt</c> concatenating every included page's Markdown body.</param>
/// <param name="EmitMarkdownSiblings">When <c>true</c>, writes a clean <c>.md</c> copy alongside each rendered page under the output root.</param>
/// <param name="EmitHeadLink">When <c>true</c>, injects a <c>&lt;link rel="alternate" type="text/markdown" href="/llms.txt"&gt;</c> tag into <c>&lt;head&gt;</c>.</param>
/// <param name="MaxSummaryBytes">Maximum UTF-8 byte length for auto-extracted page summaries in <c>llms.txt</c>.</param>
/// <param name="SiteSummary">Optional UTF-8 blockquote summary emitted near the top of <c>llms.txt</c>.</param>
/// <param name="DefaultSectionTitle">UTF-8 section heading used in <c>llms.txt</c> for root-level pages without a parent folder.</param>
[System.Diagnostics.DebuggerDisplay("LlmsOptions: Full={EmitFullText}, Siblings={EmitMarkdownSiblings}")]
public readonly record struct LlmsOptions(
    bool EmitFullText,
    bool EmitMarkdownSiblings,
    bool EmitHeadLink,
    int MaxSummaryBytes,
    byte[] SiteSummary,
    byte[] DefaultSectionTitle)
{
    /// <summary>Default maximum byte length for a page summary in <c>llms.txt</c>.</summary>
    private const int DefaultSummaryByteLimit = 160;

    /// <summary>Gets the default maximum byte length for a page summary in <c>llms.txt</c>.</summary>
    public static int DefaultMaxSummaryBytes => DefaultSummaryByteLimit;

    /// <summary>Gets the default root section heading (<c>Overview</c>).</summary>
    public static byte[] DefaultRootSectionTitle { get; } = [.. "Overview"u8];

    /// <summary>Gets the default options: emits <c>llms.txt</c>, <c>llms-full.txt</c>, <c>.md</c> siblings, and the <c>&lt;head&gt;</c> discovery link.</summary>
    public static LlmsOptions Default { get; } = new(
        true,
        true,
        true,
        DefaultMaxSummaryBytes,
        [],
        DefaultRootSectionTitle);
}
