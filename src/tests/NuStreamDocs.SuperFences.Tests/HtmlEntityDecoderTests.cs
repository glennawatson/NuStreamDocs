// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.SuperFences.Tests;

/// <summary>Branch-coverage tests for the SuperFences HtmlEntityDecoder.</summary>
public class HtmlEntityDecoderTests
{
    /// <summary>Each recognized entity decodes to its source byte.</summary>
    /// <param name="encoded">Encoded source.</param>
    /// <param name="expected">Decoded output.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("&lt;", "<")]
    [Arguments("&gt;", ">")]
    [Arguments("&amp;", "&")]
    [Arguments("&quot;", "\"")]
    [Arguments("&#39;", "'")]
    [Arguments("a&lt;b&gt;c", "a<b>c")]
    [Arguments("&amp;&amp;", "&&")]
    public async Task RecognizedEntitiesDecode(string encoded, string expected)
    {
        var decoded = HtmlEntityDecoder.Decode(Encoding.UTF8.GetBytes(encoded));
        await Assert.That(Encoding.UTF8.GetString(decoded)).IsEqualTo(expected);
    }

    /// <summary>Strings with no ampersand take the no-allocation fast-path.</summary>
    /// <param name="source">Plain text.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("plain text")]
    [Arguments("")]
    [Arguments("no entities here")]
    public async Task NoAmpersandFastPath(string source)
    {
        var decoded = HtmlEntityDecoder.Decode(Encoding.UTF8.GetBytes(source));
        await Assert.That(Encoding.UTF8.GetString(decoded)).IsEqualTo(source);
    }

    /// <summary>Unknown entities are passed through one byte at a time.</summary>
    /// <param name="source">Source containing an unrecognized entity.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("&unknown;")]
    [Arguments("&copy;")]
    [Arguments("&")]
    [Arguments("a&b")]
    public async Task UnknownEntityPassThrough(string source)
    {
        var input = Encoding.UTF8.GetBytes(source);
        var decoded = HtmlEntityDecoder.Decode(input);
        await Assert.That(Encoding.UTF8.GetString(decoded)).IsEqualTo(source);
        await Assert.That(decoded.Length).IsEqualTo(input.Length);
    }

    /// <summary>An entity at the very end of the buffer decodes correctly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EntityAtEnd()
    {
        var decoded = HtmlEntityDecoder.Decode("text&lt;"u8);
        await Assert.That(Encoding.UTF8.GetString(decoded)).IsEqualTo("text<");
    }
}
