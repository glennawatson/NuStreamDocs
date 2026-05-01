// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Byte-level UTF-8 scanner that hardens external <c>&lt;a&gt;</c>
/// anchors with <c>rel="noopener noreferrer"</c> and/or
/// <c>target="_blank"</c>. Replaces the regex-based
/// <c>ExternalAnchorRegex</c> + <c>MergeAttribute</c> pipeline.
/// </summary>
internal static class AnchorBytes
{
    /// <summary>Length of the <c>&lt;a</c> open-tag prefix consumed before attribute scanning.</summary>
    private const int AnchorOpenLength = 2;

    /// <summary>Gets the UTF-8 bytes for the literal appended when no <c>rel</c> is present.</summary>
    private static ReadOnlySpan<byte> RelAttribute => " rel=\"noopener noreferrer\""u8;

    /// <summary>Gets the UTF-8 bytes for the literal appended when no <c>target</c> is present.</summary>
    private static ReadOnlySpan<byte> TargetBlankAttribute => " target=\"_blank\""u8;

    /// <summary>Gets the UTF-8 bytes for the lowercase <c>href</c> attribute name.</summary>
    private static ReadOnlySpan<byte> HrefName => "href"u8;

    /// <summary>Gets the UTF-8 bytes for the lowercase <c>rel</c> attribute name.</summary>
    private static ReadOnlySpan<byte> RelName => "rel"u8;

    /// <summary>Gets the UTF-8 bytes for the lowercase <c>target</c> attribute name.</summary>
    private static ReadOnlySpan<byte> TargetName => "target"u8;

    /// <summary>Gets the UTF-8 bytes for the lowercase <c>http://</c> scheme prefix.</summary>
    private static ReadOnlySpan<byte> HttpPrefix => "http://"u8;

    /// <summary>Gets the UTF-8 bytes for the lowercase <c>https://</c> scheme prefix.</summary>
    private static ReadOnlySpan<byte> HttpsPrefix => "https://"u8;

    /// <summary>Gets the UTF-8 bytes for the noopener+noreferrer rel-token pair.</summary>
    private static ReadOnlySpan<byte> NoOpenerTokens => "noopener noreferrer"u8;

    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every <c>&lt;a&gt;</c> opening tag whose <c>href</c> is an absolute http(s) URL.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="addRelNoOpener">When true, merge <c>noopener noreferrer</c> into the existing <c>rel</c> attribute (or append a fresh one).</param>
    /// <param name="addTargetBlank">When true, append <c>target="_blank"</c> when no <c>target</c> attribute is already present.</param>
    /// <param name="sink">UTF-8 sink the rewritten output lands in.</param>
    /// <returns>True when at least one anchor was hardened; false when the input passed through unchanged.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, bool addRelNoOpener, bool addTargetBlank, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (!addRelNoOpener && !addTargetBlank)
        {
            return false;
        }

        var changed = false;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var open = FindNextAnchorOpen(html, cursor);
            if (open < 0)
            {
                break;
            }

            var attrsStart = open + AnchorOpenLength;
            var tagEndRel = html[attrsStart..].IndexOf((byte)'>');
            if (tagEndRel < 0)
            {
                break;
            }

            var tagEnd = attrsStart + tagEndRel;
            var attrs = html[attrsStart..tagEnd];
            if (!HasExternalHref(attrs))
            {
                cursor = tagEnd + 1;
                continue;
            }

            sink.Write(html[lastEmit..attrsStart]);
            EmitHardenedAttrs(attrs, addRelNoOpener, addTargetBlank, sink);
            sink.Write(html[tagEnd..(tagEnd + 1)]);

            lastEmit = tagEnd + 1;
            cursor = tagEnd + 1;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        sink.Write(html[lastEmit..]);
        return true;
    }

    /// <summary>Finds the next <c>&lt;a</c> open tag at or after <paramref name="cursor"/> with a proper word boundary after the <c>a</c>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="cursor">Search start offset.</param>
    /// <returns>Offset of the <c>&lt;</c>, or <c>-1</c> when no anchor remains.</returns>
    private static int FindNextAnchorOpen(ReadOnlySpan<byte> html, int cursor)
    {
        var p = cursor;
        while (p < html.Length - AnchorOpenLength)
        {
            var rel = html[p..].IndexOf((byte)'<');
            if (rel < 0)
            {
                return -1;
            }

            var lt = p + rel;
            if (lt + AnchorOpenLength >= html.Length)
            {
                return -1;
            }

            var nextByte = (byte)(html[lt + 1] | 0x20);
            if (nextByte is (byte)'a' && !ByteHelpers.IsAsciiIdentifierByte(html[lt + AnchorOpenLength]))
            {
                return lt;
            }

            p = lt + 1;
        }

        return -1;
    }

    /// <summary>Returns true when the attribute span contains an <c>href</c> whose value starts with <c>http://</c> or <c>https://</c>.</summary>
    /// <param name="attrs">Tag-body span.</param>
    /// <returns>True when the anchor points to an external absolute URL.</returns>
    private static bool HasExternalHref(ReadOnlySpan<byte> attrs)
    {
        var href = AnchorAttributeFinder.Find(attrs, HrefName);
        if (!href.Found)
        {
            return false;
        }

        return ByteHelpers.StartsWithIgnoreAsciiCase(attrs, href.ValueStart, HttpsPrefix)
            || ByteHelpers.StartsWithIgnoreAsciiCase(attrs, href.ValueStart, HttpPrefix);
    }

    /// <summary>Emits <paramref name="attrs"/> into <paramref name="sink"/> with the configured hardening applied.</summary>
    /// <param name="attrs">Tag-body span.</param>
    /// <param name="addRel">Whether to merge <c>rel</c> tokens.</param>
    /// <param name="addTargetBlank">Whether to append <c>target="_blank"</c> when missing.</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void EmitHardenedAttrs(ReadOnlySpan<byte> attrs, bool addRel, bool addTargetBlank, IBufferWriter<byte> sink)
    {
        var rel = addRel ? AnchorAttributeFinder.Find(attrs, RelName) : NamedAttribute.None;
        EmitWithRelMerge(attrs, addRel, rel, sink);

        if (!addTargetBlank)
        {
            return;
        }

        var target = AnchorAttributeFinder.Find(attrs, TargetName);
        if (target.Found)
        {
            return;
        }

        sink.Write(TargetBlankAttribute);
    }

    /// <summary>Emits the attribute span, rewriting the <c>rel</c> value when present and rel-merge is requested, or appending a fresh <c>rel</c> attribute otherwise.</summary>
    /// <param name="attrs">Tag-body span.</param>
    /// <param name="addRel">Whether rel-merge is requested.</param>
    /// <param name="rel">The located rel attribute (or <see cref="NamedAttribute.None"/>).</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void EmitWithRelMerge(ReadOnlySpan<byte> attrs, bool addRel, NamedAttribute rel, IBufferWriter<byte> sink)
    {
        if (!addRel)
        {
            sink.Write(attrs);
            return;
        }

        if (!rel.Found)
        {
            sink.Write(attrs);
            sink.Write(RelAttribute);
            return;
        }

        sink.Write(attrs[..rel.ValueStart]);
        RelTokenMerger.MergeInto(attrs[rel.ValueStart..rel.ValueEnd], NoOpenerTokens, sink);
        sink.Write(attrs[rel.ValueEnd..]);
    }
}
