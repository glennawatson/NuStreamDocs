// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Llms;

/// <summary>UTF-8 fluent configuration helpers for <see cref="LlmsOptions"/>.</summary>
public static class LlmsOptionsExtensions
{
    /// <summary>Extension members for <see cref="LlmsOptions"/>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in LlmsOptions options)
    {
        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling <c>llms-full.txt</c> generation.</summary>
        /// <param name="enabled"><c>true</c> to write <c>llms-full.txt</c>; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithEmitFullText(bool enabled) =>
            options with { EmitFullText = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling per-page <c>.md</c> sibling emission.</summary>
        /// <param name="enabled"><c>true</c> to write clean <c>.md</c> copies to the output directory; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithEmitMarkdownSiblings(bool enabled) =>
            options with { EmitMarkdownSiblings = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling the <c>&lt;head&gt;</c> alternate link tag.</summary>
        /// <param name="enabled"><c>true</c> to inject the <c>/llms.txt</c> discovery link; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithEmitHeadLink(bool enabled) =>
            options with { EmitHeadLink = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="maxSummaryBytes"/> as the summary byte budget.</summary>
        /// <param name="maxSummaryBytes">Positive maximum UTF-8 byte length.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithMaxSummaryBytes(int maxSummaryBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSummaryBytes);
            return options with { MaxSummaryBytes = maxSummaryBytes };
        }

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="siteSummary"/> as the top-level blockquote summary.</summary>
        /// <param name="siteSummary">UTF-8 summary bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithSiteSummary(byte[] siteSummary)
        {
            ArgumentNullException.ThrowIfNull(siteSummary);
            return options with { SiteSummary = siteSummary };
        }

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="siteSummary"/> as the top-level blockquote summary.</summary>
        /// <param name="siteSummary">UTF-8 summary span.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithSiteSummary(ReadOnlySpan<byte> siteSummary) =>
            options with { SiteSummary = siteSummary.ToArray() };

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="sectionTitle"/> for root-level pages.</summary>
        /// <param name="sectionTitle">UTF-8 section title bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithDefaultSectionTitle(byte[] sectionTitle)
        {
            ArgumentNullException.ThrowIfNull(sectionTitle);
            return options with { DefaultSectionTitle = sectionTitle };
        }

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="sectionTitle"/> for root-level pages.</summary>
        /// <param name="sectionTitle">UTF-8 section title span.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LlmsOptions WithDefaultSectionTitle(ReadOnlySpan<byte> sectionTitle) =>
            options with { DefaultSectionTitle = sectionTitle.ToArray() };
    }
}
