// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tests;

/// <summary>Unit tests for the YamlByteScanner helpers.</summary>
public class YamlByteScannerTests
{
    /// <summary>FrontmatterDelimiter returns the literal three-dash bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FrontmatterDelimiterIsThreeDashes() =>
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.FrontmatterDelimiter)).IsEqualTo("---");

    /// <summary>LineEnd returns past-the-newline offset and source length when no newline.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LineEndCases()
    {
        var bytes = Encoding.UTF8.GetBytes("ab\ncd");
        await Assert.That(YamlByteScanner.LineEnd(bytes, 0)).IsEqualTo(3);
        await Assert.That(YamlByteScanner.LineEnd(bytes, 3)).IsEqualTo(5);
    }

    /// <summary>TrimLeading drops space, tab, and CR.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrimLeadingDropsAsciiWhitespace()
    {
        var bytes = Encoding.UTF8.GetBytes(" \t\rfoo");
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.TrimLeading(bytes))).IsEqualTo("foo");
    }

    /// <summary>TrimWhitespace also drops trailing newlines.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrimWhitespaceDropsBothEnds()
    {
        var bytes = Encoding.UTF8.GetBytes("  foo\n");
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.TrimWhitespace(bytes))).IsEqualTo("foo");
    }

    /// <summary>Unquote strips matching paired quotes only.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnquoteVariants()
    {
        var dq = Encoding.UTF8.GetBytes("\"hi\"");
        var sq = Encoding.UTF8.GetBytes("'hi'");
        var none = Encoding.UTF8.GetBytes("hi");
        var mixed = Encoding.UTF8.GetBytes("\"hi'");
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.Unquote(dq))).IsEqualTo("hi");
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.Unquote(sq))).IsEqualTo("hi");
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.Unquote(none))).IsEqualTo("hi");
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.Unquote(mixed))).IsEqualTo("\"hi'");
    }

    /// <summary>IsTopLevelKey: every rejection branch.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IsTopLevelKeyRejections()
    {
        await Assert.That(YamlByteScanner.IsTopLevelKey([])).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes(" key: x"))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("\tkey: x"))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("# comment"))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("- item"))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("\n"))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("\r"))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("---"))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("..."))).IsFalse();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("noColon"))).IsFalse();
    }

    /// <summary>IsTopLevelKey accepts well-formed keys.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IsTopLevelKeyAccepts()
    {
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("title: Hello"))).IsTrue();
        await Assert.That(YamlByteScanner.IsTopLevelKey(Encoding.UTF8.GetBytes("a:"))).IsTrue();
    }

    /// <summary>KeyOf returns the trimmed key span; empty when no colon.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task KeyOfCases()
    {
        await Assert.That(Encoding.UTF8.GetString(YamlByteScanner.KeyOf(Encoding.UTF8.GetBytes("title : Hi")))).IsEqualTo("title");
        await Assert.That(YamlByteScanner.KeyOf(Encoding.UTF8.GetBytes("noColon")).Length).IsEqualTo(0);
    }

    /// <summary>AdvancePastValue skips indented continuations, list rows, comments, blank lines; stops at next top-level key.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AdvancePastValueIndents()
    {
        var src = Encoding.UTF8.GetBytes("  child: 1\n  - item\n# comment\n\nnext: x\n");
        var cursor = YamlByteScanner.AdvancePastValue(src, 0);
        await Assert.That(cursor).IsGreaterThan(0);

        // The cursor lands on `next: x`
        var line = Encoding.UTF8.GetString(src[cursor..]);
        await Assert.That(line.StartsWith("next:", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>AdvancePastValue stops at end of source when no following top-level key exists.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AdvancePastValueAtEnd()
    {
        var src = Encoding.UTF8.GetBytes("  child: 1\n");
        await Assert.That(YamlByteScanner.AdvancePastValue(src, 0)).IsEqualTo(src.Length);
    }
}
