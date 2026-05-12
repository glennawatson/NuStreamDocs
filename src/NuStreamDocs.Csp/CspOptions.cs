// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Csp;

/// <summary>Configuration for <c>CspPlugin</c>.</summary>
/// <param name="DefaultSrc">UTF-8 value for the <c>default-src</c> directive.</param>
/// <param name="BaseUri">UTF-8 value for the <c>base-uri</c> directive.</param>
/// <param name="FrameAncestors">UTF-8 value for the <c>frame-ancestors</c> directive.</param>
/// <param name="HashInlineScripts">When true, inline <c>&lt;script&gt;</c> bodies are hashed into <c>script-src</c>; when false, <c>script-src</c> falls back to <c>'unsafe-inline'</c>.</param>
/// <param name="HashInlineStyles">
/// When true, inline <c>&lt;style&gt;</c> bodies are hashed into <c>style-src</c>; when false (the default), <c>style-src</c> uses
/// <c>'unsafe-inline'</c> — markdown content carries <c>style="…"</c> attributes a hash can't cover.
/// </param>
/// <param name="UpgradeInsecureRequests">When true, append the <c>upgrade-insecure-requests</c> directive.</param>
/// <param name="ReportUri">UTF-8 URL for a <c>report-uri</c> directive; empty omits it.</param>
/// <param name="Mode">Enforce, or report-only.</param>
/// <param name="Enabled">Master switch; when false the plugin contributes nothing.</param>
/// <param name="ExtraSources">Extra <c>(directive, source)</c> pairs appended to that directive's source list (a new directive is added when it isn't one this plugin builds).</param>
/// <param name="ExtraDirectives">Raw directive strings appended verbatim (the escape hatch).</param>
[SuppressMessage("Major Code Smell", "S107", Justification = "A flat options record; each field is an independent CSP knob.")]
public readonly record struct CspOptions(
    byte[] DefaultSrc,
    byte[] BaseUri,
    byte[] FrameAncestors,
    bool HashInlineScripts,
    bool HashInlineStyles,
    bool UpgradeInsecureRequests,
    byte[] ReportUri,
    CspMode Mode,
    bool Enabled,
    (byte[] Directive, byte[] Source)[] ExtraSources,
    byte[][] ExtraDirectives)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static CspOptions Default { get; } = new(
        DefaultSrc: [.. "'self'"u8],
        BaseUri: [.. "'self'"u8],
        FrameAncestors: [.. "'self'"u8],
        HashInlineScripts: true,
        HashInlineStyles: false,
        UpgradeInsecureRequests: false,
        ReportUri: [],
        Mode: CspMode.Enforce,
        Enabled: true,
        ExtraSources: [],
        ExtraDirectives: []);
}
