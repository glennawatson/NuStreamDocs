// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Html;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Direct tests for <c>HtmlEmitter.ExtractInfoString</c> covering the language-tag extraction edge cases.</summary>
public class HtmlEmitterExtractInfoStringTests
{
    /// <summary>A simple <c>```csharp</c> opener yields the language token.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BacktickWithLanguage()
    {
        byte[] bytes = [.. "```csharp"u8];
        await Assert.That(Decode(bytes, openerLength: bytes.Length)).IsEqualTo("csharp");
    }

    /// <summary>A <c>~~~js</c> opener also yields the language.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TildeWithLanguage()
    {
        byte[] bytes = [.. "~~~js"u8];
        await Assert.That(Decode(bytes, openerLength: bytes.Length)).IsEqualTo("js");
    }

    /// <summary>An empty opener yields an empty info string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoLanguageYieldsEmpty()
    {
        byte[] bytes = [.. "```"u8];
        await Assert.That(Decode(bytes, openerLength: bytes.Length)).IsEqualTo(string.Empty);
    }

    /// <summary>Whitespace around the language is trimmed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LeadingWhitespaceTrimmed()
    {
        byte[] bytes = [.. "```   csharp"u8];
        await Assert.That(Decode(bytes, openerLength: bytes.Length)).IsEqualTo("csharp");
    }

    /// <summary>Extra metadata after a space is dropped — only the language survives.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExtraMetadataDropped()
    {
        byte[] bytes = [.. "```csharp title=\"hello.cs\""u8];
        await Assert.That(Decode(bytes, openerLength: bytes.Length)).IsEqualTo("csharp");
    }

    /// <summary>Wraps the helper with the small bytes/openerLength dance ExtractInfoString needs.</summary>
    /// <param name="bytes">Opener-line bytes.</param>
    /// <param name="openerLength">Length the BlockSpan should report.</param>
    /// <returns>The extracted info string as a UTF-16 string.</returns>
    private static string Decode(byte[] bytes, int openerLength)
    {
        BlockSpan opener = new(BlockKind.FencedCode, 0, openerLength, 3);
        var info = HtmlEmitter.ExtractInfoString(bytes, opener);
        return Encoding.UTF8.GetString(info);
    }
}
