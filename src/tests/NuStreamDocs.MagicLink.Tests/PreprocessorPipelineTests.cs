// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;
using NuStreamDocs.SmartSymbols;

namespace NuStreamDocs.MagicLink.Tests;

/// <summary>
/// Integration tests that exercise multi-pass preprocessor chaining
/// — the same shape <c>NuStreamDocs.Building.BuildPipeline</c>
/// uses when it threads bytes through every registered
/// <see cref="IPagePreRenderPlugin"/> in order.
/// </summary>
public class PreprocessorPipelineTests
{
    /// <summary>SmartSymbols followed by MagicLink: substituted glyphs survive into the URL-aware second pass.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SmartSymbolsThenMagicLink()
    {
        IPagePreRenderPlugin[] pipeline = [new SmartSymbolsPlugin(), new MagicLinkPlugin()];

        const string Source = "Copyright (c) 2026 — see https://example.com.";
        const string Expected = "Copyright © 2026 — see <https://example.com>.";

        await Assert.That(RunPipeline(Source, pipeline)).IsEqualTo(Expected);
    }

    /// <summary>MagicLink followed by SmartSymbols: the URL is wrapped first, and the smart-symbol pass leaves the wrapped URL alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MagicLinkThenSmartSymbols()
    {
        IPagePreRenderPlugin[] pipeline = [new MagicLinkPlugin(), new SmartSymbolsPlugin()];

        const string Source = "Visit https://example.com 1/2 of the time (c).";
        const string Expected = "Visit <https://example.com> ½ of the time ©.";

        await Assert.That(RunPipeline(Source, pipeline)).IsEqualTo(Expected);
    }

    /// <summary>Multi-plugin chain skips fenced-code regions consistently across passes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeIsHonoredAcrossEveryPass()
    {
        IPagePreRenderPlugin[] pipeline = [new SmartSymbolsPlugin(), new MagicLinkPlugin()];

        const string Source = "Outside (c) https://x.test\n```\n(c) https://x.test\n```\nAfter (c) https://x.test.";
        const string Expected = "Outside © <https://x.test>\n```\n(c) https://x.test\n```\nAfter © <https://x.test>.";

        await Assert.That(RunPipeline(Source, pipeline)).IsEqualTo(Expected);
    }

    /// <summary>Empty plugin chain leaves the source byte-for-byte unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoPreprocessorsRoundTrips()
    {
        const string Source = "Plain text with (c) and https://example.com.";
        await Assert.That(RunPipeline(Source, [])).IsEqualTo(Source);
    }

    /// <summary>Drives <paramref name="source"/> through <paramref name="pipeline"/> in declaration order, ping-ponging buffers like the build pipeline does.</summary>
    /// <param name="source">Markdown source.</param>
    /// <param name="pipeline">Preprocessor sequence.</param>
    /// <returns>Final pipeline output.</returns>
    private static string RunPipeline(string source, IPagePreRenderPlugin[] pipeline)
    {
        ReadOnlyMemory<byte> current = Encoding.UTF8.GetBytes(source);
        ArrayBufferWriter<byte>? front = null;
        ArrayBufferWriter<byte>? back = null;
        for (var i = 0; i < pipeline.Length; i++)
        {
            front ??= new(Math.Max(current.Length, 1));
            back ??= new(Math.Max(current.Length, 1));

            front.ResetWrittenCount();
            PagePreRenderContext ctx = new("p.md", current.Span, front);
            pipeline[i].PreRender(in ctx);
            current = front.WrittenMemory;
            (front, back) = (back, front);
        }

        return Encoding.UTF8.GetString(current.Span);
    }
}
