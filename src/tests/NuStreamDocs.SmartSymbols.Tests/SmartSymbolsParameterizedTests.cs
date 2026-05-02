// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace NuStreamDocs.SmartSymbols.Tests;

/// <summary>Parameterized token-variant coverage for SmartSymbolsRewriter.</summary>
public class SmartSymbolsParameterizedTests
{
    /// <summary>Each canonical token rewrites to its expected glyph.</summary>
    /// <param name="token">Source token.</param>
    /// <param name="glyph">Expected Unicode glyph.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("(c)", "©")]
    [Arguments("(C)", "©")]
    [Arguments("(r)", "®")]
    [Arguments("(R)", "®")]
    [Arguments("(tm)", "™")]
    [Arguments("(TM)", "™")]
    [Arguments("(Tm)", "™")]
    [Arguments("(tM)", "™")]
    [Arguments("+/-", "±")]
    [Arguments("=/=", "≠")]
    [Arguments("-->", "→")]
    [Arguments("<--", "←")]
    [Arguments("<-->", "↔")]
    [Arguments("==>", "⇒")]
    [Arguments("<==", "⇐")]
    [Arguments("<==>", "⇔")]
    [Arguments("c/o", "℅")]
    [Arguments("C/O", "℅")]
    [Arguments("1/2", "½")]
    [Arguments("1/4", "¼")]
    [Arguments("3/4", "¾")]
    public async Task TokenSubstitutions(string token, string glyph) =>
        await Assert.That(Rewrite($"x {token} y")).IsEqualTo($"x {glyph} y");

    /// <summary>Tokens at the start, middle, end of input all rewrite.</summary>
    /// <param name="wrapper">Format string with {0} replaced by the token.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("{0}")]
    [Arguments("a {0}")]
    [Arguments("{0} a")]
    [Arguments("a {0} b")]
    public async Task TokenAtPositions(string wrapper) =>
        await Assert.That(Rewrite(string.Format(CultureInfo.InvariantCulture, wrapper, "(c)")))
            .IsEqualTo(string.Format(CultureInfo.InvariantCulture, wrapper, "©"));

    /// <summary>Tokens that look right but lack the matching tail are not substituted.</summary>
    /// <param name="malformed">Malformed token.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("(c")]
    [Arguments("(t")]
    [Arguments("(tm")]
    [Arguments("--")]
    [Arguments("<-")]
    [Arguments("==")]
    [Arguments("+/")]
    [Arguments("c/")]
    [Arguments("1/")]
    public async Task MalformedTokensUntouched(string malformed) =>
        await Assert.That(Rewrite($"a {malformed}")).IsEqualTo($"a {malformed}");

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="input">Source text.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        SmartSymbolsRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
