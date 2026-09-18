// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Highlight.Languages.Misc;
using NuStreamDocs.Highlight.Languages.Scripting;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Behavior + boundary tests for <c>LexerRegistry</c> construction and lookup.</summary>
public class LexerRegistryTests
{
    /// <summary>Alias for an alternate custom lexer.</summary>
    private const string AlternateLanguage = "vibescript";

    /// <summary>Alias for a custom lexer.</summary>
    private const string CustomLanguage = "brainfuck";

    /// <summary>Gets the alternate custom lexer alias.</summary>
    private static ReadOnlySpan<byte> AlternateLanguageBytes => "vibescript"u8;

    /// <summary>Gets the custom lexer alias.</summary>
    private static ReadOnlySpan<byte> CustomLanguageBytes => "brainfuck"u8;

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
        const int OversizedAliasLength = 256;
        var oversized = new string('a', OversizedAliasLength);
        var ok = LexerRegistry.Default.TryGet(Encoding.UTF8.GetBytes(oversized), out _);
        await Assert.That(ok).IsFalse();
    }

    /// <summary><see cref="LexerRegistry.CreateFromStringLexers"/> registers every supplied pair on top of the built-ins.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersRegistersExtras()
    {
        var registry = LexerRegistry.CreateFromStringLexers(
            (CustomLanguage, PassThroughLexer.Instance),
            (AlternateLanguage, JavaScriptLexer.Instance));

        await Assert.That(registry.TryGet(CustomLanguageBytes, out var bf)).IsTrue();
        await Assert.That(bf).IsEqualTo(PassThroughLexer.Instance);

        await Assert.That(registry.TryGet(AlternateLanguageBytes, out var vibe)).IsTrue();
        await Assert.That(vibe).IsEqualTo(JavaScriptLexer.Instance);
    }

    /// <summary><see cref="LexerRegistry.CreateFromStringLexers"/> still resolves the built-ins it didn't override.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersKeepsBuiltIns()
    {
        var registry = LexerRegistry.CreateFromStringLexers((CustomLanguage, PassThroughLexer.Instance));
        await Assert.That(registry.TryGet("csharp"u8, out var cs)).IsTrue();
        await Assert.That(cs).IsNotNull();
    }

    /// <summary>Extras supplied with mixed-case names are matched case-insensitively against lowercase probes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersFoldsAsciiCase()
    {
        var registry = LexerRegistry.CreateFromStringLexers(("BrainFuck", PassThroughLexer.Instance));
        await Assert.That(registry.TryGet(CustomLanguageBytes, out var bf)).IsTrue();
        await Assert.That(bf).IsEqualTo(PassThroughLexer.Instance);
        await Assert.That(registry.TryGet("BRAINFUCK"u8, out var upper)).IsTrue();
        await Assert.That(upper).IsEqualTo(PassThroughLexer.Instance);
    }

    /// <summary>A later override with the same id wins.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateFromStringLexersLastWriteWins()
    {
        var registry = LexerRegistry.CreateFromStringLexers(
            (CustomLanguage, PassThroughLexer.Instance),
            (CustomLanguage, JavaScriptLexer.Instance));

        await Assert.That(registry.TryGet(CustomLanguageBytes, out var lexer)).IsTrue();
        await Assert.That(lexer).IsEqualTo(JavaScriptLexer.Instance);
    }

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
            new([.. AlternateLanguageBytes], PassThroughLexer.Instance)
        ];
        var registry = LexerRegistry.Build(entries);
        await Assert.That(registry.TryGet(AlternateLanguageBytes, out var lexer)).IsTrue();
        await Assert.That(lexer).IsEqualTo(PassThroughLexer.Instance);
    }
}
