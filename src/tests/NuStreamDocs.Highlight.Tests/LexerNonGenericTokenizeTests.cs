// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Coverage for the non-generic Lexer.Tokenize(ReadOnlySpan&lt;byte&gt;, TokenSink) overload.</summary>
public class LexerNonGenericTokenizeTests
{
    /// <summary>Non-generic Tokenize emits at least one token for non-empty source.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TokenizesViaNonGenericOverload()
    {
        var tokens = 0;
        CSharpLexer.Instance.Tokenize(
            "var x = 1;"u8,
            (_, _, _) => tokens++);
        await Assert.That(tokens).IsGreaterThan(0);
    }

    /// <summary>HighlightPlugin.OnConfigureAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PluginOnConfigure() =>
        await new HighlightPlugin().OnConfigureAsync(new("/in", "/out", []), CancellationToken.None);
}
