// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.CFamily;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Coverage for the non-generic Lexer.Tokenize(ReadOnlySpan&lt;byte&gt;, TokenSink) overload.</summary>
[System.Diagnostics.DebuggerDisplay("Tokens: {_tokenCount}")]
public class LexerNonGenericTokenizeTests
{
    /// <summary>Number of tokens received by this test instance.</summary>
    private int _tokenCount;

    /// <summary>Non-generic Tokenize emits at least one token for non-empty source.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TokenizesViaNonGenericOverload()
    {
        CSharpLexer.Instance.Tokenize(
            "var x = 1;"u8,
            RecordToken);
        await Assert.That(_tokenCount).IsGreaterThan(0);
    }

    /// <summary>Records a token received through the non-generic callback.</summary>
    /// <param name="offset">Token offset.</param>
    /// <param name="length">Token length.</param>
    /// <param name="tokenClass">Token classification.</param>
    private void RecordToken(int offset, int length, TokenClass tokenClass) => _tokenCount++;
}
