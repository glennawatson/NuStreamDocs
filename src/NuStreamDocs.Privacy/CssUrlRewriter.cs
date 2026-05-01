// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Stateless rewriter for CSS files: finds <c>url(...)</c> references
/// to absolute http(s) URLs, resolves relative URLs against the
/// stylesheet's own base, and registers + rewrites each one to its
/// local path through an <see cref="ExternalAssetRegistry"/>.
/// </summary>
/// <remarks>
/// Closes the Google Fonts loop: a fetched <c>fonts.css</c> typically
/// references <c>https://fonts.gstatic.com/.../font.woff2</c> URLs
/// inside <c>url()</c> tokens, which the per-page HTML scan never
/// sees.
/// </remarks>
internal static partial class CssUrlRewriter
{
    /// <summary>Rewrites every <c>url(...)</c> reference in <paramref name="css"/> against <paramref name="cssBaseUri"/>.</summary>
    /// <param name="css">UTF-8 CSS bytes.</param>
    /// <param name="cssBaseUri">Absolute URL the CSS file was fetched from; relative <c>url()</c> values are resolved against this.</param>
    /// <param name="registry">URL registry; new entries are appended for every external URL seen.</param>
    /// <param name="filter">Host filter; URLs whose host fails the filter are left as-is.</param>
    /// <returns>The rewritten CSS bytes.</returns>
    public static byte[] Rewrite(byte[] css, Uri cssBaseUri, ExternalAssetRegistry registry, HostFilter filter)
    {
        ArgumentNullException.ThrowIfNull(css);
        ArgumentNullException.ThrowIfNull(cssBaseUri);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(filter);

        var input = Encoding.UTF8.GetString(css);
        var output = UrlTokenRegex().Replace(input, match =>
        {
            var quote = match.Groups["q"].Value;
            var raw = match.Groups["url"].Value;
            if (!TryResolveAbsolute(raw, cssBaseUri, out var absolute) || !filter.ShouldLocalise(absolute))
            {
                return match.Value;
            }

            var local = registry.GetOrAdd(absolute);
            return $"url({quote}/{local}{quote})";
        });
        return Encoding.UTF8.GetBytes(output);
    }

    /// <summary>Resolves <paramref name="raw"/> against <paramref name="baseUri"/> when it's relative, or accepts it as-is when it's already an absolute http(s) URL.</summary>
    /// <param name="raw">Raw URL text from the <c>url()</c> token.</param>
    /// <param name="baseUri">Absolute URL the CSS file was fetched from.</param>
    /// <param name="absolute">Resolved absolute URL on success.</param>
    /// <returns>True when <paramref name="absolute"/> was set.</returns>
    private static bool TryResolveAbsolute(string raw, Uri baseUri, out string absolute)
    {
        absolute = string.Empty;
        if (string.IsNullOrEmpty(raw) || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var asAbsolute))
        {
            if (asAbsolute.Scheme is not ("http" or "https"))
            {
                return false;
            }

            absolute = asAbsolute.AbsoluteUri;
            return true;
        }

        if (!Uri.TryCreate(baseUri, raw, out var resolved) || resolved.Scheme is not ("http" or "https"))
        {
            return false;
        }

        absolute = resolved.AbsoluteUri;
        return true;
    }

    /// <summary>Matches a CSS <c>url(...)</c> token, optionally with single or double quotes.</summary>
    /// <returns>Compiled regex.</returns>
    [GeneratedRegex("""url\(\s*(?<q>"|'|)(?<url>[^)"']+)\k<q>\s*\)""", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UrlTokenRegex();
}
