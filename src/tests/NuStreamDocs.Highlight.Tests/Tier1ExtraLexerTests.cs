// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the second batch of Tier-1 lexers (Swift, SQL, Dockerfile, PHP, Ruby, Markdown).</summary>
public class Tier1ExtraLexerTests
{
    /// <summary>Swift classifies <c>func</c>/<c>guard</c>/<c>let</c>/<c>var</c> and primitive types correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SwiftClassifiesKeywordsAndTypes()
    {
        var html = SwiftLexer.Instance.Render("func greet(name: String) -> String { let prefix: String = \"hi\"; return prefix + name; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">func</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">let</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kt\">String</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">return</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Swift multi-line string <c>"""..."""</c> classifies as a single double-string token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SwiftClassifiesMultilineString()
    {
        var html = SwiftLexer.Instance.Render("let msg = \"\"\"\nhi\n\"\"\""u8);
        await Assert.That(html.Contains("<span class=\"s2\">&quot;&quot;&quot;\nhi\n&quot;&quot;&quot;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>SQL classifies <c>SELECT</c> / <c>FROM</c> / <c>WHERE</c> case-insensitively.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SqlClassifiesKeywordsCaseInsensitively()
    {
        var html = SqlLexer.Instance.Render("Select * From users Where id = 1"u8);
        await Assert.That(html.Contains("<span class=\"k\">Select</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">From</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">Where</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>SQL classifies <c>--</c> line comments and <c>'doubled''quote'</c> string escapes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SqlClassifiesLineCommentAndDoubledQuoteString()
    {
        var html = SqlLexer.Instance.Render("-- comment\nINSERT INTO t VALUES ('it''s ok')"u8);
        await Assert.That(html.Contains("<span class=\"c1\">-- comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s1\">'it''s ok'</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Dockerfile classifies instruction verbs at line start.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DockerfileClassifiesInstructions()
    {
        var html = DockerfileLexer.Instance.Render("FROM debian:12\nRUN apt-get update\nCMD [\"bash\"]\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">FROM</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">RUN</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">CMD</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>PHP open tag classifies as preprocessor; <c>$variable</c> as a name; <c>echo</c> as a keyword.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PhpClassifiesOpenTagAndVariable()
    {
        var html = PhpLexer.Instance.Render("<?php\necho $name;\nfunction greet() {}"u8);
        await Assert.That(html.Contains("<span class=\"cp\">&lt;?php</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">echo</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">function</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Ruby classifies <c>def</c>/<c>end</c>, instance / class variables, and symbol literals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RubyClassifiesSigilsAndSymbols()
    {
        var html = RubyLexer.Instance.Render("def greet\n  @name = :hello\n  @@count += 1\nend"u8);
        await Assert.That(html.Contains("<span class=\"kd\">def</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">end</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">@name</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">@@count</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s1\">:hello</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cm\">", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>Ruby block comment <c>=begin</c>/<c>=end</c> classifies as <c>cm</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RubyClassifiesEqualBlockComment()
    {
        var html = RubyLexer.Instance.Render("=begin\nhello\n=end\nx = 1"u8);
        await Assert.That(html.Contains("<span class=\"cm\">=begin\nhello\n=end</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Markdown classifies ATX heading prefixes and fence markers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MarkdownClassifiesHeadingsAndFences()
    {
        var html = MarkdownLexer.Instance.Render("# Title\n## Subtitle\n```rust\nfn x() {}\n```\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\"># Title</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">## Subtitle</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cp\">```rust</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cp\">```</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Markdown classifies bullet markers and inline code spans.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MarkdownClassifiesBulletAndInlineCode()
    {
        var html = MarkdownLexer.Instance.Render("- item with `code` inside\n"u8);
        await Assert.That(html.Contains("<span class=\"o\">- </span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"s1\">`code`</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Detector resolves an obvious Swift block by its signatures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetectorResolvesSwift()
    {
        const string Source = "import SwiftUI\nstruct ContentView: View {\n  var body: some View { Text(\"Hello\") }\n}";
        var escaped = System.Text.Encoding.UTF8.GetBytes(System.Net.WebUtility.HtmlEncode(Source));
        var resolved = LanguageDetector.TryDetect(escaped, LexerRegistry.Default, [], out var languageId);
        var languageString = System.Text.Encoding.UTF8.GetString(languageId);
        await Assert.That(resolved).IsTrue();
        await Assert.That(languageString).IsEqualTo("swift");
    }

    /// <summary>Detector resolves an obvious SQL block by its signatures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetectorResolvesSql()
    {
        const string Source = "SELECT name, count FROM users WHERE active = 1 GROUP BY name ORDER BY count DESC;";
        var escaped = System.Text.Encoding.UTF8.GetBytes(System.Net.WebUtility.HtmlEncode(Source));
        var resolved = LanguageDetector.TryDetect(escaped, LexerRegistry.Default, [], out var languageId);
        var languageString = System.Text.Encoding.UTF8.GetString(languageId);
        await Assert.That(resolved).IsTrue();
        await Assert.That(languageString).IsEqualTo("sql");
    }

    /// <summary>Detector resolves an obvious Dockerfile block by its signatures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetectorResolvesDockerfile()
    {
        const string Source = "FROM debian:12\nWORKDIR /app\nCOPY . .\nRUN apt-get update\nCMD [\"bash\"]";
        var escaped = System.Text.Encoding.UTF8.GetBytes(System.Net.WebUtility.HtmlEncode(Source));
        var resolved = LanguageDetector.TryDetect(escaped, LexerRegistry.Default, [], out var languageId);
        var languageString = System.Text.Encoding.UTF8.GetString(languageId);
        await Assert.That(resolved).IsTrue();
        await Assert.That(languageString).IsEqualTo("dockerfile");
    }

    /// <summary>Detector resolves an obvious PHP block by its signatures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetectorResolvesPhp()
    {
        const string Source = "<?php\nnamespace App;\nfunction greet($name) { echo $name; }\n?>";
        var escaped = System.Text.Encoding.UTF8.GetBytes(System.Net.WebUtility.HtmlEncode(Source));
        var resolved = LanguageDetector.TryDetect(escaped, LexerRegistry.Default, [], out var languageId);
        var languageString = System.Text.Encoding.UTF8.GetString(languageId);
        await Assert.That(resolved).IsTrue();
        await Assert.That(languageString).IsEqualTo("php");
    }
}
