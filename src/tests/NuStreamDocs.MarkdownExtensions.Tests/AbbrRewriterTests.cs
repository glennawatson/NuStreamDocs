// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Abbr;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Verifies abbreviation matching against UTF-8 source tokens.</summary>
public sealed class AbbrRewriterTests
{
    /// <summary>Tokens match exactly while preserving their spelling and case.</summary>
    /// <param name="source">Markdown containing an abbreviation definition and its uses.</param>
    /// <param name="expected">Expected rewritten markdown.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("*[API]: interface\nAPI api Api", "<abbr title=\"interface\">API</abbr> api Api")]
    [Arguments("*[\u00c9CO]: ecology\n\u00c9CO \u00e9co", "<abbr title=\"ecology\">\u00c9CO</abbr> \u00e9co")]
    [Arguments("*[API]: first\n*[API]: second\nAPI", "<abbr title=\"second\">API</abbr>")]
    [Arguments("*[API]: interface\nXAPI APIX `API` [API](url) API", "XAPI APIX `API` [API](url) <abbr title=\"interface\">API</abbr>")]
    public async Task RewriteMatchesExactToken(string source, string expected)
    {
        ArrayBufferWriter<byte> output = new();
        AbbrRewriter.Rewrite(Encoding.UTF8.GetBytes(source), output);
        await Assert.That(Encoding.UTF8.GetString(output.WrittenSpan)).IsEqualTo(expected);
    }
}
