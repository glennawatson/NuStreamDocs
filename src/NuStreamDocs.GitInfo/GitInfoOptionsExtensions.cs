// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.GitInfo;

/// <summary>Fluent configuration helpers for <see cref="GitInfoOptions"/>.</summary>
public static class GitInfoOptionsExtensions
{
    /// <summary>Extension members for <see cref="GitInfoOptions"/>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in GitInfoOptions options)
    {
        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling page footer emission.</summary>
        /// <param name="enabled"><c>true</c> to emit the revision footer; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GitInfoOptions WithEmitFooter(bool enabled) =>
            options with { EmitFooter = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling contributor name display.</summary>
        /// <param name="enabled"><c>true</c> to include contributors; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GitInfoOptions WithEmitContributors(bool enabled) =>
            options with { EmitContributors = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling <c>dcterms.modified</c> meta emission.</summary>
        /// <param name="enabled"><c>true</c> to inject the meta tag; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GitInfoOptions WithEmitMetaTag(bool enabled) =>
            options with { EmitMetaTag = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling <c>sitemap.xml</c> <c>&lt;lastmod&gt;</c> enrichment.</summary>
        /// <param name="enabled"><c>true</c> to enrich <c>sitemap.xml</c>; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GitInfoOptions WithEnrichSitemap(bool enabled) =>
            options with { EnrichSitemap = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling bundled stylesheet emission.</summary>
        /// <param name="enabled"><c>true</c> to emit and link <c>git-info.css</c>; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GitInfoOptions WithIncludeStylesheet(bool enabled) =>
            options with { IncludeStylesheet = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="maxContributors"/> as the contributor limit.</summary>
        /// <param name="maxContributors">Positive contributor cap.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GitInfoOptions WithMaxContributors(int maxContributors)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxContributors);
            return options with { MaxContributors = maxContributors };
        }

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="provider"/> to read Git history.</summary>
        /// <param name="provider">Custom log provider.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GitInfoOptions WithLogProvider(IGitLogProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            return options with { LogProvider = provider };
        }
    }
}
