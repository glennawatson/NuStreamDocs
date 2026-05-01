// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Lightbox.Tests;

/// <summary>Behaviour tests for <c>ImageWrapper</c>.</summary>
public class ImageWrapperTests
{
    /// <summary>Standalone images get wrapped in a glightbox anchor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WrapsStandaloneImage()
    {
        var input = "<p><img src=\"/img/foo.png\" alt=\"foo\"></p>"u8;
        var sink = new ArrayBufferWriter<byte>();
        var wrapped = ImageWrapper.Rewrite(input, "glightbox", sink);

        var result = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(wrapped).IsEqualTo(1);
        await Assert.That(result).IsEqualTo("<p><a href=\"/img/foo.png\" class=\"glightbox\"><img src=\"/img/foo.png\" alt=\"foo\"></a></p>");
    }

    /// <summary>Images that are already inside an anchor are left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsImagesAlreadyInsideAnchor()
    {
        var input = "<a href=\"https://example.com\"><img src=\"/img/foo.png\"></a>"u8;
        var sink = new ArrayBufferWriter<byte>();
        var wrapped = ImageWrapper.Rewrite(input, "glightbox", sink);

        var result = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(wrapped).IsEqualTo(0);
        await Assert.That(result).IsEqualTo("<a href=\"https://example.com\"><img src=\"/img/foo.png\"></a>");
    }

    /// <summary>Multiple standalone images all get wrapped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WrapsEveryStandaloneImage()
    {
        var input = "<img src=\"a.png\"><img src=\"b.png\">"u8;
        var sink = new ArrayBufferWriter<byte>();
        var wrapped = ImageWrapper.Rewrite(input, "glightbox", sink);

        var result = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(wrapped).IsEqualTo(2);
        await Assert.That(result.Contains("href=\"a.png\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Contains("href=\"b.png\"", StringComparison.Ordinal)).IsTrue();
    }
}
