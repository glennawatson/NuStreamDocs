// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Mermaid;

/// <summary>Configuration for <see cref="MermaidPlugin"/>.</summary>
/// <param name="CloudflareRocketLoaderOptOut">
/// When true, the runtime script carries <c>data-cfasync="false"</c> so Cloudflare Rocket Loader leaves it alone.
/// </param>
[System.Diagnostics.DebuggerDisplay("MermaidOptions: {ToString(),nq}")]
public sealed record MermaidOptions(bool CloudflareRocketLoaderOptOut)
{
    /// <summary>Gets the default mermaid ES module jsDelivr pin.</summary>
    public static byte[] DefaultRuntimeUrl { get; } =
        [.. "https://cdn.jsdelivr.net/npm/mermaid@12.0.0/dist/mermaid.esm.min.mjs"u8];

    /// <summary>Gets the default options: Cloudflare Rocket Loader opt-out enabled, default runtime URL.</summary>
    public static MermaidOptions Default => new(true);

    /// <summary>Gets the UTF-8 absolute URL of the mermaid ES module the page imports.</summary>
    public byte[] RuntimeUrl { get; init; } = DefaultRuntimeUrl;
}
