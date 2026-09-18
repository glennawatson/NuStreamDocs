// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Tests;

/// <summary>Tests reference-link label normalization.</summary>
public sealed class LinkReferenceRewriterTests
{
    /// <summary>Label matching preserves case folding across scratch-buffer sizes.</summary>
    /// <param name="length">Label length in bytes.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(255)]
    [Arguments(256)]
    [Arguments(257)]
    [Arguments(4096)]
    public async Task Rewrite_NormalizesLabelsAcrossBufferSizes(int length)
    {
        var reference = new string('A', length);
        var definition = new string('a', length);
        var source = Encoding.UTF8.GetBytes($"[text][{reference}]\n[{definition}]: /guide\n");

        var result = LinkReferenceRewriter.Rewrite(source);

        await Assert.That(Encoding.UTF8.GetString(result)).IsEqualTo("[text](/guide)\n");
    }
}
