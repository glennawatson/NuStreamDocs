// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.MarkdownExtensions.AttrList;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Branch-coverage tests for AttrListParser.</summary>
public class AttrListParserBranchTests
{
    /// <summary>Empty input yields all-empty result.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput()
    {
        var (id, classes, kv) = AttrListParser.Parse(string.Empty);
        await Assert.That(id).IsEqualTo(string.Empty);
        await Assert.That(classes.Count).IsEqualTo(0);
        await Assert.That(kv.Count).IsEqualTo(0);
    }

    /// <summary>Whitespace-only input yields all-empty result.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespaceInput()
    {
        var (id, classes, kv) = AttrListParser.Parse("   \t  ");
        await Assert.That(id).IsEqualTo(string.Empty);
        await Assert.That(classes.Count).IsEqualTo(0);
        await Assert.That(kv.Count).IsEqualTo(0);
    }

    /// <summary>ID, classes and kv all parsed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FullCombo()
    {
        var (id, classes, kv) = AttrListParser.Parse("#hero .a .b key1=\"value with spaces\" key2=plain key3='single'");
        await Assert.That(id).IsEqualTo("hero");
        await Assert.That(classes.Count).IsEqualTo(2);
        await Assert.That(classes[0]).IsEqualTo("a");
        await Assert.That(kv.Count).IsEqualTo(3);
        await Assert.That(kv[0].Value).IsEqualTo("value with spaces");
        await Assert.That(kv[1].Value).IsEqualTo("plain");
        await Assert.That(kv[2].Value).IsEqualTo("single");
    }

    /// <summary>Empty class token (.[space]) is skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyClassToken()
    {
        var (_, classes, _) = AttrListParser.Parse(". .real");
        await Assert.That(classes.Count).IsEqualTo(1);
        await Assert.That(classes[0]).IsEqualTo("real");
    }

    /// <summary>Bare key (no = sign) is recorded as a flag attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareKeyFlag()
    {
        var (_, _, kv) = AttrListParser.Parse("disabled");
        await Assert.That(kv.Count).IsEqualTo(1);
        await Assert.That(kv[0].Key).IsEqualTo("disabled");
        await Assert.That(kv[0].Value).IsEqualTo(string.Empty);
    }

    /// <summary>Key with trailing equals but no value yields empty value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyValueAfterEquals()
    {
        var (_, _, kv) = AttrListParser.Parse("k=");
        await Assert.That(kv.Count).IsEqualTo(1);
        await Assert.That(kv[0].Value).IsEqualTo(string.Empty);
    }

    /// <summary>Quoted value missing the closing quote consumes to end of input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedQuotedValue()
    {
        var (_, _, kv) = AttrListParser.Parse("k=\"never closed");
        await Assert.That(kv.Count).IsEqualTo(1);
        await Assert.That(kv[0].Value).IsEqualTo("never closed");
    }
}
