// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Behavior + boundary tests for <c>LexerRegistry</c> construction and lookup.</summary>
public class LexerRegistryTests
{
    /// <summary>The built-in registry resolves a representative built-in alias byte-shaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultResolvesBuiltInAliasFromBytes()
    {
        var ok = LexerRegistry.Default.TryGet("csharp"u8, out var lexer);
        await Assert.That(ok).IsTrue();
        await Assert.That(lexer).IsNotNull();
    }

    /// <summary>Lookup folds ASCII case so <c>"CSharp"</c> hits the same entry as <c>"csharp"</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultLookupIsCaseInsensitive()
    {
        var ok = LexerRegistry.Default.TryGet("CSharp"u8, out var lexer);
        await Assert.That(ok).IsTrue();
        await Assert.That(lexer).IsNotNull();
    }

    /// <summary>Unknown alias misses without throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownAliasMisses()
    {
        var ok = LexerRegistry.Default.TryGet("not-a-real-language"u8, out var lexer);
        await Assert.That(ok).IsFalse();
        await Assert.That(lexer).IsNull();
    }

    /// <summary>An empty alias misses regardless of registry contents.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyAliasMisses()
    {
        var ok = LexerRegistry.Default.TryGet(default, out var lexer);
        await Assert.That(ok).IsFalse();
        await Assert.That(lexer).IsNull();
    }

    /// <summary>Aliases longer than the longest registered key miss without an out-of-bounds probe.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LongAliasMissesCleanly()
    {
        string oversized = new('a', 256);
        var ok = LexerRegistry.Default.TryGet(Encoding.UTF8.GetBytes(oversized), out _);
        await Assert.That(ok).IsFalse();
    }

    /// <summary><see cref="LexerRegistry.CreateFromStringLexers"/> registers every supplied pair on top of the built-ins.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersRegistersExtras()
    {
        var registry = LexerRegistry.CreateFromStringLexers(
            ("brainfuck", PassThroughLexer.Instance),
            ("vibescript", JavaScriptLexer.Instance));

        await Assert.That(registry.TryGet("brainfuck"u8, out var bf)).IsTrue();
        await Assert.That(bf).IsEqualTo((Lexer)PassThroughLexer.Instance);

        await Assert.That(registry.TryGet("vibescript"u8, out var vibe)).IsTrue();
        await Assert.That(vibe).IsEqualTo((Lexer)JavaScriptLexer.Instance);
    }

    /// <summary><see cref="LexerRegistry.CreateFromStringLexers"/> still resolves the built-ins it didn't override.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersKeepsBuiltIns()
    {
        var registry = LexerRegistry.CreateFromStringLexers(("brainfuck", PassThroughLexer.Instance));
        await Assert.That(registry.TryGet("csharp"u8, out var cs)).IsTrue();
        await Assert.That(cs).IsNotNull();
    }

    /// <summary>Extras supplied with mixed-case names are matched case-insensitively against lowercase probes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersFoldsAsciiCase()
    {
        var registry = LexerRegistry.CreateFromStringLexers(("BrainFuck", PassThroughLexer.Instance));
        await Assert.That(registry.TryGet("brainfuck"u8, out var bf)).IsTrue();
        await Assert.That(bf).IsEqualTo((Lexer)PassThroughLexer.Instance);
        await Assert.That(registry.TryGet("BRAINFUCK"u8, out var upper)).IsTrue();
        await Assert.That(upper).IsEqualTo((Lexer)PassThroughLexer.Instance);
    }

    /// <summary>A later override with the same id wins.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersLastWriteWins()
    {
        var registry = LexerRegistry.CreateFromStringLexers(
            ("brainfuck", PassThroughLexer.Instance),
            ("brainfuck", JavaScriptLexer.Instance));

        await Assert.That(registry.TryGet("brainfuck"u8, out var lexer)).IsTrue();
        await Assert.That(lexer).IsEqualTo((Lexer)JavaScriptLexer.Instance);
    }

    /// <summary>Null pair array throws <see cref="ArgumentNullException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersNullThrows() =>
        await Assert.That(static () => LexerRegistry.CreateFromStringLexers(null!))
            .Throws<ArgumentNullException>();

    /// <summary>Empty pair array throws <see cref="ArgumentOutOfRangeException"/> — the byte-keyed path expects at least one entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersEmptyThrows() =>
        await Assert.That(static () => LexerRegistry.CreateFromStringLexers())
            .Throws<ArgumentOutOfRangeException>();

    /// <summary>The byte-keyed <see cref="LexerRegistry.Build(LexerNameValue[])"/> overload accepts pre-encoded entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildAcceptsPreEncodedEntries()
    {
        LexerNameValue[] entries =
        [
            new([.. "vibescript"u8], PassThroughLexer.Instance)
        ];
        var registry = LexerRegistry.Build(entries);
        await Assert.That(registry.TryGet("vibescript"u8, out var lexer)).IsTrue();
        await Assert.That(lexer).IsEqualTo((Lexer)PassThroughLexer.Instance);
    }
}
