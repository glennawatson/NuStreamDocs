// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Csp;

/// <summary>Fluent helpers for building <see cref="CspOptions"/>.</summary>
public static class CspOptionsExtensions
{
    /// <summary>Adds <paramref name="source"/> to the source list of CSP directive <paramref name="directive"/> (e.g. allow a CDN origin under <c>script-src</c>).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="directive">UTF-8 directive name (e.g. <c>script-src</c>).</param>
    /// <param name="source">UTF-8 source expression (an origin, a scheme, or a keyword).</param>
    /// <returns>The updated options.</returns>
    public static CspOptions AllowSource(this in CspOptions options, ReadOnlySpan<byte> directive, ReadOnlySpan<byte> source) =>
        options with { ExtraSources = ArrayJoiner.Concat(options.ExtraSources, [(directive.ToArray(), source.ToArray())]) };

    /// <summary>Replaces the <c>default-src</c> value.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 directive value.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithDefaultSrc(this in CspOptions options, ReadOnlySpan<byte> value) =>
        options with { DefaultSrc = value.ToArray() };

    /// <summary>Replaces the <c>frame-ancestors</c> value.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 directive value.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithFrameAncestors(this in CspOptions options, ReadOnlySpan<byte> value) =>
        options with { FrameAncestors = value.ToArray() };

    /// <summary>Sets a <c>report-uri</c> destination.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="reportUri">UTF-8 URL violations are reported to.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithReportUri(this in CspOptions options, ReadOnlySpan<byte> reportUri) =>
        options with { ReportUri = reportUri.ToArray() };

    /// <summary>Switches the policy to report-only (violations are reported, not blocked).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithReportOnly(this in CspOptions options) =>
        options with { Mode = CspMode.ReportOnly };

    /// <summary>Stops hashing inline scripts; <c>script-src</c> then uses <c>'unsafe-inline'</c> (looser, but needed if the site has inline event-handler attributes).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithoutScriptHashing(this in CspOptions options) =>
        options with { HashInlineScripts = false };

    /// <summary>Hashes inline <c>&lt;style&gt;</c> bodies into <c>style-src</c> instead of allowing <c>'unsafe-inline'</c> (<c>style="…"</c> attributes are still blocked).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithInlineStyleHashing(this in CspOptions options) =>
        options with { HashInlineStyles = true };

    /// <summary>Appends the <c>upgrade-insecure-requests</c> directive.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithUpgradeInsecureRequests(this in CspOptions options) =>
        options with { UpgradeInsecureRequests = true };

    /// <summary>Appends a raw directive verbatim (e.g. <c>"worker-src 'self'"u8</c>).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="directive">UTF-8 directive (name plus values, without a trailing semicolon).</param>
    /// <returns>The updated options.</returns>
    public static CspOptions WithExtraDirective(this in CspOptions options, ReadOnlySpan<byte> directive) =>
        options with { ExtraDirectives = ArrayJoiner.Concat(options.ExtraDirectives, [directive.ToArray()]) };

    /// <summary>Disables the plugin (it then contributes nothing).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static CspOptions Disable(this in CspOptions options) =>
        options with { Enabled = false };
}
