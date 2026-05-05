// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Direct unit tests for the byte-only <c>AttrListMarker.EmitMerged</c> hot path.</summary>
public class AttrListMarkerEmitMergedTests
{
    /// <summary>An attr-list providing only an id emits a leading <c>id="..."</c> attribute when none exists.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IdOnlyWhenNoExisting()
    {
        var merged = EmitMerged(string.Empty, "#intro");
        await Assert.That(merged).IsEqualTo(" id=\"intro\"");
    }

    /// <summary>An attr-list id replaces an existing id at its original position.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IdReplacesExisting()
    {
        var merged = EmitMerged(" id=\"old\" data-x=\"y\"", "#new");
        await Assert.That(merged).IsEqualTo(" id=\"new\" data-x=\"y\"");
    }

    /// <summary>An attr-list class is appended to an existing class attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClassAppendsToExisting()
    {
        var merged = EmitMerged(" class=\"a\"", ".b .c");
        await Assert.That(merged).IsEqualTo(" class=\"a b c\"");
    }

    /// <summary>An attr-list kv pair replaces an existing same-named attribute and escapes <c>&amp;</c> in the value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task KeyValueReplacesAndEscapes()
    {
        var merged = EmitMerged(" data-x=\"old\"", "data-x=\"a&b\"");
        await Assert.That(merged).IsEqualTo(" data-x=\"a&amp;b\"");
    }

    /// <summary>Bare flag-style attributes (<c>{: download }</c>) emit as bare attributes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareKeyEmitsBareAttribute()
    {
        var merged = EmitMerged(" href=\"/x\"", "download");
        await Assert.That(merged).IsEqualTo(" href=\"/x\" download");
    }

    /// <summary>Multiple class tokens append in declaration order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleClassTokensInOrder()
    {
        var merged = EmitMerged(string.Empty, ".one .two .three");
        await Assert.That(merged).IsEqualTo(" class=\"one two three\"");
    }

    /// <summary>Existing attributes without overrides flow through untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PassesThroughUnaffectedAttributes()
    {
        var merged = EmitMerged(" data-a=\"1\" data-b=\"2\"", "#x");
        await Assert.That(merged).IsEqualTo(" data-a=\"1\" data-b=\"2\" id=\"x\"");
    }

    /// <summary>Runs <c>EmitMerged</c> over a synthetic source span and returns the UTF-8 result as a string.</summary>
    /// <remarks>The synthetic span carries both the existing-attrs window and the attr-list body at known offsets, mirroring how the rewriter walks an opening tag.</remarks>
    /// <param name="existingAttrs">Existing attribute fragment (with leading space when non-empty).</param>
    /// <param name="attrListBody">Attr-list body contents (between the <c>{:</c> and <c>}</c> markers).</param>
    /// <returns>Merged attribute fragment as a string.</returns>
    private static string EmitMerged(string existingAttrs, string attrListBody)
    {
        var source = Encoding.UTF8.GetBytes(existingAttrs + "{:" + attrListBody + "}");
        const int ExistingStart = 0;
        var existingEnd = existingAttrs.Length;
        var attrListStart = existingEnd + 2; // skip "{:"
        var attrListEnd = source.Length - 1; // skip "}"

        ArrayBufferWriter<byte> sink = new();
        AttrListMarker.EmitMerged(source, ExistingStart, existingEnd, attrListStart, attrListEnd, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
