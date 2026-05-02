// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.MagicLink.Tests;

/// <summary>Parameterized tests for MagicLinkRewriter scheme + trailing-punctuation handling.</summary>
public class MagicLinkSchemeTests
{
    /// <summary>Each recognized scheme produces an autolink.</summary>
    /// <param name="url">URL with a recognized scheme.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("https://x.test")]
    [Arguments("http://x.test")]
    [Arguments("ftps://x.test/file")]
    [Arguments("ftp://x.test/file")]
    [Arguments("mailto:user@x.test")]
    public async Task RecognizedSchemesWrap(string url) =>
        await Assert.That(Rewrite($"see {url} here")).Contains($"<{url}>");

    /// <summary>Trailing punctuation is peeled off the wrapped URL.</summary>
    /// <param name="suffix">Punctuation byte appended after the URL.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(".")]
    [Arguments(",")]
    [Arguments(";")]
    [Arguments(":")]
    [Arguments("!")]
    [Arguments("?")]
    [Arguments(")")]
    [Arguments("]")]
    [Arguments("}")]
    public async Task TrailingPunctuationPeeled(string suffix)
    {
        var output = Rewrite($"see https://x.test{suffix}");
        await Assert.That(output).Contains("<https://x.test>");
        await Assert.That(output).EndsWith(suffix);
    }

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        MagicLinkRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
