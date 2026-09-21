// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The assertion behind every generated pinned-output row.</summary>
internal static class PinnedOutputAssertion
{
    /// <summary>Asserts that rendering <paramref name="markdown"/> produces the expected HTML, ignoring the differences the parity check ignores.</summary>
    /// <param name="markdown">Markdown source.</param>
    /// <param name="expectedHtml">Pinned HTML.</param>
    /// <returns>The assertion task.</returns>
    internal static async Task RendersAsync(string markdown, string expectedHtml)
    {
        var rendered = OursRenderer.Render(markdown);
        await Assert.That(rendered.Error).IsNull();
        await Assert.That(HtmlNormalizer.Normalize(rendered.Html!)).IsEqualTo(HtmlNormalizer.Normalize(expectedHtml));
    }
}
