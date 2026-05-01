// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Optimise.Tests;

/// <summary>Branch-coverage edge cases for HtmlMinifier.</summary>
public class HtmlMinifierBranchTests
{
    /// <summary>Tag without a closing > is copied to end-of-input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedTagCopiedToEnd() =>
        await Assert.That(Minify("<div ")).Contains("<div");

    /// <summary>Comment without a closing --> consumes to end-of-input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedComment() =>
        await Assert.That(Minify("a<!-- never ends")).IsEqualTo("a");

    /// <summary>Style tag content is preserved (whitespace + comment-like markers).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreservesStyleContent()
    {
        const string Input = "<style>  body { color: red; /* note */ }\n  </style>";
        await Assert.That(Minify(Input)).IsEqualTo(Input);
    }

    /// <summary>Textarea preserves whitespace.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreservesTextarea()
    {
        const string Input = "<textarea>  raw   text\n</textarea>";
        await Assert.That(Minify(Input)).IsEqualTo(Input);
    }

    /// <summary>A &lt;pre that is not actually the pre tag (e.g. &lt;preX) does not preserve.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NotAPreserveTag() =>
        await Assert.That(Minify("<preX>  spaced  </preX>")).Contains("spaced");

    /// <summary>Mixed-case preserve tag is recognised case-insensitively.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedCasePreserve()
    {
        const string Input = "<PRE>  raw\n  </PRE>";
        await Assert.That(Minify(Input)).IsEqualTo(Input);
    }

    /// <summary>Preserve block without an end tag is copied to end-of-input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnterminatedPreserve()
    {
        var result = Minify("<pre>  never ends");
        await Assert.That(result).Contains("<pre>  never ends");
    }

    /// <summary>Drives the minifier with default options.</summary>
    /// <param name="html">Source.</param>
    /// <returns>Minified output.</returns>
    private static string Minify(string html)
    {
        var sink = new ArrayBufferWriter<byte>(Math.Max(1, html.Length));
        HtmlMinifier.Minify(Encoding.UTF8.GetBytes(html), sink, HtmlMinifyOptions.Default);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
