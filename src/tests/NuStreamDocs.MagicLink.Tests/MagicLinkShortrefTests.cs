// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.MagicLink.Tests;

/// <summary>Behavior tests for the GitHub-shortref expansion path of <c>MagicLinkRewriter</c>.</summary>
public class MagicLinkShortrefTests
{
    /// <summary>Default test repo bytes.</summary>
    private static readonly byte[] DefaultRepo = "reactiveui/ReactiveUI"u8.ToArray();

    /// <summary>A bare <c>#NNN</c> shortref expands to a Markdown link against the configured repo.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareIssueRefExpandsToMarkdownLink() =>
        await Assert.That(Rewrite("see #377", expandMentions: false))
            .IsEqualTo("see [#377](https://github.com/reactiveui/ReactiveUI/issues/377)");

    /// <summary>Issue refs inside parentheses expand without consuming the trailing <c>)</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IssueRefInsideParenthesesPreservesParen() =>
        await Assert.That(Rewrite("(PR #382)", expandMentions: false))
            .IsEqualTo("(PR [#382](https://github.com/reactiveui/ReactiveUI/issues/382))");

    /// <summary>An <c>@user</c> mention at a word boundary expands to a profile-page Markdown link.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UserMentionExpandsToProfileLink() =>
        await Assert.That(Rewrite("thanks @oliverw!", expandMentions: true))
            .IsEqualTo("thanks [@oliverw](https://github.com/oliverw)!");

    /// <summary>An <c>@</c> immediately following a word character is left untouched (avoids email rewrites).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AtSignInEmailIsNotRewritten() =>
        await Assert.That(Rewrite("contact foo@bar.com today", expandMentions: true))
            .IsEqualTo("contact foo@bar.com today");

    /// <summary>A <c>#</c> immediately following a word character (e.g. <c>foo#1</c>) is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HashAttachedToWordIsNotRewritten() =>
        await Assert.That(Rewrite("note xx#377 here", expandMentions: false))
            .IsEqualTo("note xx#377 here");

    /// <summary>Shortrefs inside fenced code blocks are preserved verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShortrefsInFencedCodeArePreserved()
    {
        const string Source = "```\nsee #1 and @bot\n```\n";
        await Assert.That(Rewrite(Source, expandMentions: true)).IsEqualTo(Source);
    }

    /// <summary>Shortrefs inside inline code are preserved verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShortrefsInInlineCodeArePreserved() =>
        await Assert.That(Rewrite("see `#1` ref", expandMentions: false))
            .IsEqualTo("see `#1` ref");

    /// <summary>Shortrefs inside an existing Markdown link's bracket span are left alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShortrefsInExistingLinkPreserved() =>
        await Assert.That(Rewrite("[issue #1](other)", expandMentions: false))
            .IsEqualTo("[issue #1](other)");

    /// <summary>A bare <c>#</c> followed by a non-digit byte is left as text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HashWithoutDigitsIsLiteral() =>
        await Assert.That(Rewrite("section # heading", expandMentions: false))
            .IsEqualTo("section # heading");

    /// <summary>When <c>defaultRepo</c> is empty, <c>#NNN</c> is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IssueRefWithoutRepoIsLiteral()
    {
        var bytes = Encoding.UTF8.GetBytes("see #377");
        ArrayBufferWriter<byte> sink = new(bytes.Length);
        MagicLinkRewriter.Rewrite(bytes, sink, [], false);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("see #377");
    }

    /// <summary>Rewrites <paramref name="input"/> with shortref expansion enabled.</summary>
    /// <param name="input">Markdown source.</param>
    /// <param name="expandMentions">Whether to expand <c>@user</c> mentions.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input, bool expandMentions)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        MagicLinkRewriter.Rewrite(bytes, sink, DefaultRepo, expandMentions);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
