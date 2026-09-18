// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Csp;

/// <summary>Fluent helpers for building <see cref="CspOptions"/>.</summary>
public static class CspOptionsExtensions
{
    /// <summary>Extension members for <c>CspOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in CspOptions options)
    {
        /// <summary>Adds <paramref name="source"/> to the source list of CSP directive <paramref name="directive"/> (e.g. allow a CDN origin under <c>script-src</c>).</summary>
        /// <param name="directive">UTF-8 directive name (e.g. <c>script-src</c>).</param>
        /// <param name="source">UTF-8 source expression (an origin, a scheme, or a keyword).</param>
        /// <returns>The updated options.</returns>
        public CspOptions AllowSource(
            ReadOnlySpan<byte> directive,
            ReadOnlySpan<byte> source) =>
            options with
            {
                ExtraSources = ArrayJoiner.Concat(options.ExtraSources, [(directive.ToArray(), source.ToArray())])
            };

        /// <summary>Replaces the <c>default-src</c> value.</summary>
        /// <param name="value">UTF-8 directive value.</param>
        /// <returns>The updated options.</returns>
        public CspOptions WithDefaultSrc(ReadOnlySpan<byte> value) =>
            options with { DefaultSrc = value.ToArray() };

        /// <summary>Replaces the <c>frame-ancestors</c> value.</summary>
        /// <param name="value">UTF-8 directive value.</param>
        /// <returns>The updated options.</returns>
        public CspOptions WithFrameAncestors(ReadOnlySpan<byte> value) =>
            options with { FrameAncestors = value.ToArray() };

        /// <summary>Sets a <c>report-uri</c> destination.</summary>
        /// <param name="reportUri">UTF-8 URL violations are reported to.</param>
        /// <returns>The updated options.</returns>
        public CspOptions WithReportUri(ReadOnlySpan<byte> reportUri) =>
            options with { ReportUri = reportUri.ToArray() };

        /// <summary>Switches the policy to report-only (violations are reported, not blocked).</summary>
        /// <returns>The updated options.</returns>
        public CspOptions WithReportOnly() =>
            options with { Mode = CspMode.ReportOnly };

        /// <summary>Stops hashing inline scripts; <c>script-src</c> then uses <c>'unsafe-inline'</c> (looser, but needed if the site has inline event-handler attributes).</summary>
        /// <returns>The updated options.</returns>
        public CspOptions WithoutScriptHashing() =>
            options with { HashInlineScripts = false };

        /// <summary>Hashes inline <c>&lt;style&gt;</c> bodies into <c>style-src</c> instead of allowing <c>'unsafe-inline'</c> (<c>style="…"</c> attributes are still blocked).</summary>
        /// <returns>The updated options.</returns>
        public CspOptions WithInlineStyleHashing() =>
            options with { HashInlineStyles = true };

        /// <summary>Appends the <c>upgrade-insecure-requests</c> directive.</summary>
        /// <returns>The updated options.</returns>
        public CspOptions WithUpgradeInsecureRequests() =>
            options with { UpgradeInsecureRequests = true };

        /// <summary>Appends a raw directive verbatim (e.g. <c>"worker-src 'self'"u8</c>).</summary>
        /// <param name="directive">UTF-8 directive (name plus values, without a trailing semicolon).</param>
        /// <returns>The updated options.</returns>
        public CspOptions WithExtraDirective(ReadOnlySpan<byte> directive) =>
            options with { ExtraDirectives = ArrayJoiner.Concat(options.ExtraDirectives, [directive.ToArray()]) };

        /// <summary>Disables the plugin (it then contributes nothing).</summary>
        /// <returns>The updated options.</returns>
        public CspOptions Disable() =>
            options with { Enabled = false };
    }
}
