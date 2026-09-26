// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Html;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the site-wide Cloudflare Rocket Loader opt-out.</summary>
public class RocketLoaderOptOutTests
{
    /// <summary>Extra sink capacity for inserted attributes.</summary>
    private const int OutputHeadroom = 64;

    /// <summary>Script start tags gain the opt-out attribute; everything else is copied verbatim.</summary>
    /// <param name="input">Page HTML.</param>
    /// <param name="expected">Rewritten HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<p>x</p>", "<p>x</p>")]
    [Arguments("<script>a()</script>", "<script data-cfasync=\"false\">a()</script>")]
    [Arguments(
        "<script src=\"/a.js\" defer></script>",
        "<script data-cfasync=\"false\" src=\"/a.js\" defer></script>")]
    [Arguments(
        "<script\ntype=\"module\">m()</script>",
        "<script data-cfasync=\"false\"\ntype=\"module\">m()</script>")]
    [Arguments(
        "<script type=\"module\" data-cfasync=\"false\">m()</script>",
        "<script type=\"module\" data-cfasync=\"false\">m()</script>")]
    [Arguments("<scripts>x</scripts>", "<scripts>x</scripts>")]
    [Arguments(
        "<script>document.write(\"<script src='x'></scr\" + \"ipt>\")</script><p>after</p>",
        "<script data-cfasync=\"false\">document.write(\"<script src='x'></scr\" + \"ipt>\")</script><p>after</p>")]
    [Arguments(
        "<script data-x=\"a>b\">c()</script>",
        "<script data-cfasync=\"false\" data-x=\"a>b\">c()</script>")]
    [Arguments(
        "<head><script>a()</script></head><body><script src=\"/b.js\"></script></body>",
        "<head><script data-cfasync=\"false\">a()</script></head><body><script data-cfasync=\"false\" src=\"/b.js\"></script></body>")]
    [Arguments("<p>tail <script", "<p>tail <script")]
    [Arguments("<script src=\"x", "<script src=\"x")]
    [Arguments("<script>unterminated()", "<script data-cfasync=\"false\">unterminated()")]
    public async Task RewritesScriptStartTags(string input, string expected)
    {
        ArrayBufferWriter<byte> sink = new(input.Length + OutputHeadroom);
        RocketLoaderOptOutRewriter.Rewrite(Encoding.UTF8.GetBytes(input), sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(expected);
    }

    /// <summary>Pages without a script skip the rewrite pass.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NeedsRewriteOnlyWhenScriptPresent()
    {
        await Assert.That(RocketLoaderOptOutRewriter.NeedsRewrite("<p>plain</p>"u8)).IsFalse();
        await Assert.That(RocketLoaderOptOutRewriter.NeedsRewrite("<script></script>"u8)).IsTrue();
    }

    /// <summary>The opt-out is on by default and can be switched off.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuilderToggle()
    {
        await Assert.That(new DocBuilder().RocketLoaderOptOutEnabled).IsTrue();
        await Assert.That(new DocBuilder().UseRocketLoaderOptOut(false).RocketLoaderOptOutEnabled).IsFalse();
    }

    /// <summary>A build adds the opt-out to scripts on emitted pages by default, and leaves them alone when disabled.</summary>
    /// <param name="enabled">Toggle value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task BuildAppliesOptOut(bool enabled, CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Input, "index.md"),
            "# Home\n\n<script src=\"/x.js\"></script>\n",
            cancellationToken);

        _ = await new DocBuilder()
            .WithInput(fixture.Input)
            .WithOutput(fixture.Output)
            .UseRocketLoaderOptOut(enabled)
            .BuildAsync(cancellationToken);

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "index.html"), cancellationToken);
        await Assert.That(html.Contains("<script data-cfasync=\"false\" src=\"/x.js\">", StringComparison.Ordinal)).IsEqualTo(enabled);
        await Assert.That(html.Contains("<script src=\"/x.js\">", StringComparison.Ordinal)).IsEqualTo(!enabled);
    }
}
