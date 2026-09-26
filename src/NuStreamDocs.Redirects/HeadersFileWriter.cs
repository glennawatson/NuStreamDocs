// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Redirects;

/// <summary>Formats <see cref="HeaderRule"/>s into the Netlify / Cloudflare-Pages <c>_headers</c> file, and supplies the default cache rules.</summary>
public static class HeadersFileWriter
{
    /// <summary>Gets the one-week cache header applied to generated assets that aren't content-hashed.</summary>
    private static ReadOnlySpan<byte> OneWeekCache => "Cache-Control: public, max-age=604800"u8;

    /// <summary>
    /// Returns the default cache rules: a one-week cache for the generated asset directories under <c>/assets/</c>, and an
    /// immutable cache for the content-hashed <c>/assets/fonts/*</c>.
    /// </summary>
    /// <returns>The default rule blocks, in emit order.</returns>
    /// <remarks>
    /// The patterns never overlap: Netlify and Cloudflare Pages apply every matching rule and comma-join repeated headers,
    /// so a broad <c>/assets/*</c> rule would give font files two conflicting <c>Cache-Control</c> values.
    /// </remarks>
    public static HeaderRule[] DefaultRules() =>
    [
        new([.. "/assets/javascripts/*"u8], [[.. OneWeekCache]]),
        new([.. "/assets/stylesheets/*"u8], [[.. OneWeekCache]]),
        new([.. "/assets/images/*"u8], [[.. OneWeekCache]]),
        new([.. "/assets/extra/*"u8], [[.. OneWeekCache]]),
        new([.. "/assets/extensions/*"u8], [[.. OneWeekCache]]),
        new([.. "/assets/external/*"u8], [[.. OneWeekCache]]),
        new([.. "/assets/fonts/*"u8], [[.. "Cache-Control: public, max-age=31536000, immutable"u8]])
    ];

    /// <summary>Writes the <c>_headers</c> file content for <paramref name="rules"/> (each block: pattern line, then two-space-indented header lines, then a blank line).</summary>
    /// <param name="rules">Rule blocks, in emit order.</param>
    /// <param name="writer">Destination.</param>
    public static void WriteHeadersFile(IReadOnlyList<HeaderRule> rules, IBufferWriter<byte> writer)
    {
        for (var r = 0; r < rules.Count; r++)
        {
            writer.Write(rules[r].PathPattern);
            writer.Write("\n"u8);
            var lines = rules[r].HeaderLines;
            for (var i = 0; i < lines.Length; i++)
            {
                writer.Write("  "u8);
                writer.Write(lines[i]);
                writer.Write("\n"u8);
            }

            writer.Write("\n"u8);
        }
    }
}
