// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.MarkdownExtensions.Admonitions;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Tests for GitHub blockquote alert syntax (<c>&gt; [!NOTE]</c>, <c>&gt; [!TIP]</c>, <c>&gt; [!IMPORTANT]</c>, <c>&gt; [!WARNING]</c>, <c>&gt; [!CAUTION]</c>).</summary>
public sealed class GitHubAlertRewriterTests
{
    /// <summary>Verifies all five GitHub alert types (case-insensitive) render matching admonition classes and titles.</summary>
    /// <param name="marker">GitHub alert marker token.</param>
    /// <param name="expectedClass">Expected CSS class.</param>
    /// <param name="expectedTitle">Expected default title.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("NOTE", "note", "Note")]
    [Arguments("tip", "tip", "Tip")]
    [Arguments("Important", "important", "Important")]
    [Arguments("WARNING", "warning", "Warning")]
    [Arguments("caution", "caution", "Caution")]
    public async Task Rewrite_RecognizedGitHubAlertTypes_EmitsMatchingAdmonition(
        string marker,
        string expectedClass,
        string expectedTitle)
    {
        var md = $"   > \t [!{marker}] \t\r\n> First line.\n>\n>\tTab-prefixed line.\n\nFollowing paragraph.\n";
        var html = Rewrite(md, true);
        await Assert.That(html).Contains($"<div class=\"admonition {expectedClass}\">");
        await Assert.That(html).Contains($"<p class=\"admonition-title\">{expectedTitle}</p>");
        await Assert.That(html).Contains("First line.\n\nTab-prefixed line.");
        await Assert.That(html).Contains("Following paragraph.");
    }

    /// <summary>Verifies plain blockquotes, unknown markers, trailing text, unclosed brackets, and fenced code blocks stay untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rewrite_NonAlertBlockquotesAndFencedCode_PassThroughUnchanged()
    {
        const string input = """
            > Plain blockquote line.

            > [!NOTE] extra words on header line
            > Stays a blockquote.

            > [!UNKNOWN]
            > Unknown alert type stays a blockquote.

            > [!UNCLOSED
            > Missing bracket.

            > []
            > Empty brackets.

            > [!]
            > Empty alert name.

            ```markdown
            > [!WARNING]
            > Inside a fenced code block!
            ```
            """;

        await Assert.That(Rewrite(input, true)).IsEqualTo(input);

        var eofHeaderOnly = Rewrite("> [!NOTE]", true);
        await Assert.That(eofHeaderOnly).Contains("<div class=\"admonition note\">");
        var eofBareQuote = Rewrite("> [!NOTE]\n>Tight\n>", true);
        await Assert.That(eofBareQuote).Contains("Tight\n");
        var trailingSpacesEof = Rewrite("> [!NOTE]\n  ", true);
        await Assert.That(trailingSpacesEof).Contains("<div class=\"admonition note\">");
        await Assert.That(Rewrite("    > [!NOTE]\n", true)).IsEqualTo("    > [!NOTE]\n");
        await Assert.That(Rewrite(">   ", true)).IsEqualTo(">   ");
        await Assert.That(Rewrite("> [!a", true)).IsEqualTo("> [!a");
    }

    /// <summary>Verifies <see cref="AdmonitionOptions.GitHubAlerts"/> opt-out, plugin <c>NeedsRewrite</c>, and <c>DocBuilder.UseAdmonitions</c> overloads.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AdmonitionPluginAndBuilder_OptOutAndGuards_RespectGitHubAlertsFlag()
    {
        await Assert.That(static () => new AdmonitionPlugin(null!)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UseAdmonitions((AdmonitionOptions)null!)).Throws<ArgumentNullException>();
        await Assert.That(static () => new DocBuilder().UseAdmonitions((Func<AdmonitionOptions, AdmonitionOptions>)null!)).Throws<ArgumentNullException>();

        AdmonitionPlugin enabled = new();
        AdmonitionPlugin disabled = new(AdmonitionOptions.Default with { GitHubAlerts = false });

        await Assert.That(enabled.PreRenderPriority).IsEqualTo(PluginPriority.Normal);
        await Assert.That(enabled.NeedsRewrite("> [!WARNING]\n> Body\n"u8)).IsTrue();
        await Assert.That(enabled.NeedsRewrite("Plain paragraph.\n"u8)).IsFalse();
        await Assert.That(disabled.NeedsRewrite("> [!WARNING]\n> Body\n"u8)).IsFalse();
        await Assert.That(disabled.NeedsRewrite("!!! note\n    Body\n"u8)).IsTrue();

        ArrayBufferWriter<byte> disabledOut = new();
        PagePreRenderContext ctx = new("page.md", "> [!WARNING]\n> Kept literal.\n"u8, disabledOut);
        disabled.PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(disabledOut.WrittenSpan)).IsEqualTo("> [!WARNING]\n> Kept literal.\n");

        var configuredBuilder = new DocBuilder()
            .UseAdmonitions(AdmonitionOptions.Default)
            .UseAdmonitions(static opts => opts with { GitHubAlerts = false });
        await Assert.That(configuredBuilder).IsNotNull();
    }

    /// <summary>Runs <see cref="AdmonitionRewriter"/> and decodes the resulting UTF-8 bytes.</summary>
    /// <param name="markdown">Input Markdown.</param>
    /// <param name="gitHubAlerts">Whether GitHub alerts are enabled.</param>
    /// <returns>Rewritten Markdown/HTML string.</returns>
    private static string Rewrite(string markdown, bool gitHubAlerts)
    {
        ArrayBufferWriter<byte> sink = new();
        AdmonitionRewriter.Rewrite(Encoding.UTF8.GetBytes(markdown), gitHubAlerts, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
