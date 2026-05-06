// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Scripting;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the Perl lexer.</summary>
public class PerlLexerTests
{
    /// <summary>Perl classifies <c>sub</c>/<c>my</c>/<c>our</c>/<c>package</c>/<c>use</c> as declarations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesDeclarationKeywords()
    {
        var html = PerlLexer.Instance.Render("package My::Module;\nuse strict;\nsub greet { my $name = shift; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">package</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">use</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">sub</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">my</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies <c>$scalar</c>, <c>@array</c>, <c>%hash</c> sigil variables as <c>n</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesSigilVariables()
    {
        var html = PerlLexer.Instance.Render("my $name = $ARGV[0]; my @items = (1, 2); my %map = ();"u8);
        await Assert.That(html.Contains("<span class=\"n\">$name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$ARGV</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">@items</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">%map</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies special single-byte sigil variables (<c>$_</c>, <c>$1</c>, <c>$@</c>).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesSpecialSigilVariables()
    {
        var html = PerlLexer.Instance.Render("print $_; print $1; print $@;"u8);
        await Assert.That(html.Contains("<span class=\"n\">$_</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$1</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$@</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies <c>q{...}</c> / <c>qq{...}</c> / <c>qw{...}</c> quote-like operators as a single string token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesQuoteLikeOperators()
    {
        var html = PerlLexer.Instance.Render("my $s = q{plain};\nmy $t = qq(interp $name);\nmy @w = qw[a b c];"u8);
        await Assert.That(html.Contains("<span class=\"s2\">q{plain}</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">qq(interp $name)</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">qw[a b c]</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies <c>m/.../</c> regex match and <c>qr/.../flags</c> compiled-regex literals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesRegexQuoteLikes()
    {
        var html = PerlLexer.Instance.Render("if ($x =~ m/foo/i) { } my $r = qr/bar/sm;"u8);
        await Assert.That(html.Contains("<span class=\"s2\">m/foo/i</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">qr/bar/sm</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies <c>s/from/to/flags</c> substitution as a single string token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesSubstitution()
    {
        var html = PerlLexer.Instance.Render("$x =~ s/old/new/g;\n$y =~ tr/a-z/A-Z/;"u8);
        await Assert.That(html.Contains("<span class=\"s2\">s/old/new/g</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s2\">tr/a-z/A-Z/</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies <c>=pod</c> ... <c>=cut</c> blocks as <c>cm</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesPodBlocks()
    {
        var html = PerlLexer.Instance.Render("=head1 NAME\nMy::Module - example\n=cut\nuse strict;"u8);
        await Assert.That(html.Contains("<span class=\"cm\">=head1 NAME\nMy::Module - example\n=cut</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies heredoc introducers as a string token; the body bytes pass through.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesHeredocIntroducer()
    {
        var html = PerlLexer.Instance.Render("my $text = <<EOF;\nhello world\nEOF"u8);
        await Assert.That(html.Contains("<span class=\"s2\">&lt;&lt;EOF</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies <c>#</c> line comments and word operators (<c>eq</c>, <c>ne</c>).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesCommentsAndWordOperators()
    {
        var html = PerlLexer.Instance.Render("# top comment\nif ($a eq $b) { } unless ($x ne 'y') { }"u8);
        await Assert.That(html.Contains("<span class=\"c1\"># top comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">if</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">eq</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">unless</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">ne</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies <c>__FILE__</c>/<c>__LINE__</c>/<c>undef</c> as constants.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesConstants()
    {
        var html = PerlLexer.Instance.Render("print __FILE__, __LINE__; my $x = undef;"u8);
        await Assert.That(html.Contains("<span class=\"kc\">__FILE__</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">__LINE__</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">undef</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Perl classifies built-in functions (<c>print</c>, <c>chomp</c>, <c>split</c>, <c>map</c>) as <c>nb</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PerlClassifiesBuiltins()
    {
        var html = PerlLexer.Instance.Render("chomp $line; my @parts = split /,/, $line; print join('-', @parts);"u8);
        await Assert.That(html.Contains("<span class=\"nb\">chomp</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">split</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">print</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">join</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the Perl aliases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesPerlAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "perl"u8], out var pl)).IsTrue();
        await Assert.That(pl).IsSameReferenceAs(PerlLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "pl"u8], out var pl2)).IsTrue();
        await Assert.That(pl2).IsSameReferenceAs(PerlLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "pm"u8], out var pm)).IsTrue();
        await Assert.That(pm).IsSameReferenceAs(PerlLexer.Instance);
    }
}
