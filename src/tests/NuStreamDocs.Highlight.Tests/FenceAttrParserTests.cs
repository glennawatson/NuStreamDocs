// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Tests for the <see cref="FenceAttrParser"/> helper.</summary>
public class FenceAttrParserTests
{
    /// <summary>Recognizes the <c>linenums</c> attribute.</summary>
    /// <param name="info">Fence-info string.</param>
    /// <param name="expected">Expected value string.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("linenums=1", "1")]
    [Arguments("linenums=\"10\"", "10")]
    [Arguments("linenums='5'", "5")]
    [Arguments("other=val linenums=20", "20")]
    [Arguments("linenums=30 other=val", "30")]
    public async Task TryGetLineNums_finds_value(string info, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(info);
        var success = FenceAttrParser.TryGetLineNums(bytes, out var value);
        var valueString = success ? Encoding.UTF8.GetString(value) : null;
        await Assert.That(success).IsTrue();
        await Assert.That(valueString).IsEqualTo(expected);
    }

    /// <summary>Returns false when <c>linenums</c> is missing or malformed.</summary>
    /// <param name="info">Fence-info string.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("line-nums=1")]
    [Arguments("linenums")]
    [Arguments("linenums=")]
    public async Task TryGetLineNums_returns_false_on_miss(string info)
    {
        var bytes = Encoding.UTF8.GetBytes(info);
        var success = FenceAttrParser.TryGetLineNums(bytes, out _);
        await Assert.That(success).IsFalse();
    }

    /// <summary>Recognizes the <c>hl_lines</c> attribute.</summary>
    /// <param name="info">Fence-info string.</param>
    /// <param name="expected">Expected value string.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("hl_lines=2", "2")]
    [Arguments("hl_lines=\"1 3-5\"", "1 3-5")]
    [Arguments("hl_lines='2 4'", "2 4")]
    [Arguments("other=val hl_lines=1", "1")]
    public async Task TryGetHighlightLines_finds_value(string info, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(info);
        var success = FenceAttrParser.TryGetHighlightLines(bytes, out var value);
        var valueString = success ? Encoding.UTF8.GetString(value) : null;
        await Assert.That(success).IsTrue();
        await Assert.That(valueString).IsEqualTo(expected);
    }

    /// <summary>Recognizes the <c>title</c> attribute.</summary>
    /// <param name="info">Fence-info string.</param>
    /// <param name="expected">Expected value string.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("title=foo", "foo")]
    [Arguments("title=\"My Title\"", "My Title")]
    public async Task TryGetTitle_finds_value(string info, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(info);
        var success = FenceAttrParser.TryGetTitle(bytes, out var value);
        var valueString = success ? Encoding.UTF8.GetString(value) : null;
        await Assert.That(success).IsTrue();
        await Assert.That(valueString).IsEqualTo(expected);
    }
}
