// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Data;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the INI / TOML / .properties lexers built on top of <c>IniFamilyRules</c>.</summary>
public class IniFamilyLexerTests
{
    /// <summary>INI classifies <c>[section]</c>, <c>;</c>/<c>#</c> comments, key-equals-value pairs, and quoted values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IniClassifiesSectionsCommentsKeysAndValues()
    {
        var html = IniLexer.Instance.Render(";comment\n#hash\n[section]\nkey = \"hello\"\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\">;comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">#hash</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nc\">[section]</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">key</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">=</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">&quot;hello&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>TOML classifies double-bracket array-of-tables headers and boolean constants.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TomlClassifiesDoubleBracketAndBooleans()
    {
        var html = TomlLexer.Instance.Render("[[server]]\nport = 8080\nenabled = true\nname = \"prod\"\n"u8);
        await Assert.That(html.Contains("<span class=\"nc\">[[server]]</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">port</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"mi\">8080</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">true</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">&quot;prod&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>TOML <c>#</c> comments classify but <c>;</c> does not (TOML doesn't recognize semicolon comments).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TomlOnlyHashComments()
    {
        var html = TomlLexer.Instance.Render("# hash comment\n; not a comment\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\"># hash comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">; not a comment</span>", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>Properties classifies <c>!</c> and <c>#</c> comments, accepts both <c>=</c> and <c>:</c> separators.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PropertiesClassifiesBothSeparators()
    {
        var html = PropertiesLexer.Instance.Render("# hash\n! bang\nkey1 = value\nkey2 : value2\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\"># hash</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\">! bang</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">key1</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"na\">key2</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">=</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">:</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves <c>editorconfig</c> / <c>gitconfig</c> / <c>systemd</c> aliases to <see cref="IniLexer"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesIniAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "editorconfig"u8], out var editorconfig)).IsTrue();
        await Assert.That(editorconfig).IsSameReferenceAs(IniLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "gitconfig"u8], out var gitconfig)).IsTrue();
        await Assert.That(gitconfig).IsSameReferenceAs(IniLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "properties"u8], out var properties)).IsTrue();
        await Assert.That(properties).IsSameReferenceAs(PropertiesLexer.Instance);

        await Assert.That(LexerRegistry.Default.TryGet([.. "toml"u8], out var toml)).IsTrue();
        await Assert.That(toml).IsSameReferenceAs(TomlLexer.Instance);
    }
}
