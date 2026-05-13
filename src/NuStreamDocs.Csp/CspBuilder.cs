// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Csp;

/// <summary>Builds the <c>Content-Security-Policy</c> directive string for a page from its inline-block hashes and the configured options.</summary>
public static class CspBuilder
{
    /// <summary>Builds the CSP value (the <c>content="…"</c> string) for one page.</summary>
    /// <param name="scriptHashes"><c>'sha256-…'</c> tokens for the page's inline scripts (used only when <see cref="CspOptions.HashInlineScripts"/> is true).</param>
    /// <param name="styleHashes"><c>'sha256-…'</c> tokens for the page's inline styles (used only when <see cref="CspOptions.HashInlineStyles"/> is true).</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The CSP value bytes.</returns>
    public static byte[] Build(
        IReadOnlyList<byte[]> scriptHashes,
        IReadOnlyList<byte[]> styleHashes,
        in CspOptions options)
    {
        var directives = new List<Directive>
        {
            new([.. "default-src"u8], [options.DefaultSrc]),
            new([.. "base-uri"u8], [options.BaseUri]),
            new([.. "object-src"u8], [[.. "'none'"u8]]),
            new([.. "frame-ancestors"u8], [options.FrameAncestors]),
            new([.. "img-src"u8], [[.. "'self'"u8], [.. "data:"u8]]),
            new([.. "font-src"u8], [[.. "'self'"u8]]),
            new([.. "style-src"u8], InlineSourceList(options.HashInlineStyles, styleHashes)),
            new([.. "script-src"u8], InlineSourceList(options.HashInlineScripts, scriptHashes))
        };

        for (var i = 0; i < options.ExtraSources.Length; i++)
        {
            FindOrAdd(directives, options.ExtraSources[i].Directive).Sources.Add(options.ExtraSources[i].Source);
        }

        ArrayBufferWriter<byte> sink = new();
        var first = true;
        for (var i = 0; i < directives.Count; i++)
        {
            WriteSeparator(sink, ref first);
            sink.Write(directives[i].Name);
            for (var s = 0; s < directives[i].Sources.Count; s++)
            {
                sink.Write(" "u8);
                sink.Write(directives[i].Sources[s]);
            }
        }

        for (var i = 0; i < options.ExtraDirectives.Length; i++)
        {
            WriteSeparator(sink, ref first);
            sink.Write(options.ExtraDirectives[i]);
        }

        if (options.UpgradeInsecureRequests)
        {
            WriteSeparator(sink, ref first);
            sink.Write("upgrade-insecure-requests"u8);
        }

        if (options.ReportUri is [_, ..])
        {
            WriteSeparator(sink, ref first);
            sink.Write("report-uri "u8);
            sink.Write(options.ReportUri);
        }

        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Returns <c>['self', …hashes]</c> when hashing is on, otherwise <c>['self', 'unsafe-inline']</c>.</summary>
    /// <param name="hash">Whether hashing is enabled for this directive.</param>
    /// <param name="hashes">The page's hash tokens.</param>
    /// <returns>The source list.</returns>
    private static List<byte[]> InlineSourceList(bool hash, IReadOnlyList<byte[]> hashes)
    {
        List<byte[]> sources = [[.. "'self'"u8]];
        if (!hash)
        {
            sources.Add([.. "'unsafe-inline'"u8]);
            return sources;
        }

        for (var i = 0; i < hashes.Count; i++)
        {
            sources.Add(hashes[i]);
        }

        return sources;
    }

    /// <summary>Finds the directive named <paramref name="name"/> in <paramref name="directives"/>, adding an empty one when absent.</summary>
    /// <param name="directives">The directive list.</param>
    /// <param name="name">UTF-8 directive name.</param>
    /// <returns>The matching (or newly added) directive.</returns>
    private static Directive FindOrAdd(List<Directive> directives, byte[] name)
    {
        for (var i = 0; i < directives.Count; i++)
        {
            if (directives[i].Name.AsSpan().SequenceEqual(name))
            {
                return directives[i];
            }
        }

        Directive added = new(name, []);
        directives.Add(added);
        return added;
    }

    /// <summary>Writes <c>"; "</c> before every directive except the first.</summary>
    /// <param name="sink">Destination.</param>
    /// <param name="first">Tracks whether the next write is the first directive.</param>
    private static void WriteSeparator(IBufferWriter<byte> sink, ref bool first)
    {
        if (!first)
        {
            sink.Write("; "u8);
        }

        first = false;
    }

    /// <summary>One CSP directive being assembled.</summary>
    private sealed record Directive(byte[] Name, List<byte[]> Sources);
}
