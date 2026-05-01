// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Stateless collector that pulls inline <c>&lt;style&gt;</c> and
/// <c>&lt;script&gt;</c> bodies out of rendered HTML and adds their
/// SHA-256 hashes (formatted for a Content-Security-Policy directive)
/// to a thread-safe set.
/// </summary>
internal static partial class CspHashCollector
{
    /// <summary>Returns true when <paramref name="html"/> contains any inline style or script tag worth hashing.</summary>
    /// <param name="html">Page HTML.</param>
    /// <returns>True when the cheap pre-filter matches.</returns>
    public static bool MayHaveInlineBlocks(ReadOnlySpan<byte> html) =>
        html.IndexOf("<style"u8) >= 0 || html.IndexOf("<script"u8) >= 0;

    /// <summary>Hashes every inline style and script body in <paramref name="html"/> and adds the formatted CSP source to <paramref name="styles"/> / <paramref name="scripts"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="styles">Sink for <c>'sha256-…'</c> tokens from inline <c>&lt;style&gt;</c> blocks.</param>
    /// <param name="scripts">Sink for <c>'sha256-…'</c> tokens from inline <c>&lt;script&gt;</c> blocks.</param>
    public static void Collect(ReadOnlySpan<byte> html, ConcurrentDictionary<string, byte> styles, ConcurrentDictionary<string, byte> scripts)
    {
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(scripts);

        var input = Encoding.UTF8.GetString(html);
        AddHashes(StyleBlockRegex().Matches(input), styles);
        AddHashes(ScriptBlockRegex().Matches(input), scripts);
    }

    /// <summary>Computes the SHA-256 hash of each match's body capture and adds the CSP-formatted source to <paramref name="sink"/>.</summary>
    /// <param name="matches">Match collection (must capture the body in a group named <c>body</c>).</param>
    /// <param name="sink">Output set.</param>
    private static void AddHashes(MatchCollection matches, ConcurrentDictionary<string, byte> sink)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        for (var i = 0; i < matches.Count; i++)
        {
            var body = matches[i].Groups["body"].Value;
            if (body is [])
            {
                continue;
            }

            SHA256.HashData(Encoding.UTF8.GetBytes(body), hash);
            sink.TryAdd($"'sha256-{Convert.ToBase64String(hash)}'", 0);
        }
    }

    /// <summary>Matches an inline <c>&lt;style&gt;</c> block (<c>script</c> with attributes referencing <c>src</c> would not have an inline body).</summary>
    /// <returns>Compiled regex.</returns>
    [GeneratedRegex(@"<style\b[^>]*>(?<body>.*?)</style>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleBlockRegex();

    /// <summary>Matches an inline <c>&lt;script&gt;</c> block; <c>src</c>-only scripts have an empty body and are skipped.</summary>
    /// <returns>Compiled regex.</returns>
    [GeneratedRegex(@"<script\b[^>]*>(?<body>.*?)</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptBlockRegex();
}
