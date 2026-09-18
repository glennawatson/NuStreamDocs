// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.AspNetCore.Http;

namespace NuStreamDocs.Serve.Tests;

/// <summary>Verifies response cancellation throughout HTML injection and pass-through.</summary>
public sealed class HtmlInjectionMiddlewareTests
{
    /// <summary>Content type that enables script injection.</summary>
    private const string HtmlContentType = "text/html";

    /// <summary>HTML whose closing body tag requires three response writes.</summary>
    private const string HtmlBody = "<body>hello</body>";

    /// <summary>Every response operation observes request cancellation and restores the original stream.</summary>
    /// <param name="contentType">Response content type.</param>
    /// <param name="body">Buffered response content.</param>
    /// <param name="abortAfterWrites">Writes allowed before request cancellation; zero aborts before copying.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("text/plain", "plain", 0)]
    [Arguments(HtmlContentType, "hello", 0)]
    [Arguments(HtmlContentType, "hello", 1)]
    [Arguments(HtmlContentType, HtmlBody, 0)]
    [Arguments(HtmlContentType, HtmlBody, 1)]
    [Arguments(HtmlContentType, HtmlBody, 2)]
    public async Task InvokeAsync_RequestAborted_StopsWriting(
        string contentType,
        string body,
        int abortAfterWrites,
        CancellationToken cancellationToken)
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var output = new RequestAbortStream(request, abortAfterWrites);
        var context = new DefaultHttpContext { RequestAborted = request.Token };
        context.Response.ContentType = contentType;
        context.Response.Body = output;
        var bytes = Encoding.UTF8.GetBytes(body);
        if (abortAfterWrites is 0)
        {
            await request.CancelAsync();
        }

        await Assert.That(() => HtmlInjectionMiddleware.InvokeAsync(
            context,
            ctx => ctx.Response.Body.WriteAsync(bytes, CancellationToken.None).AsTask())).Throws<OperationCanceledException>();

        await Assert.That(output.WriteCount).IsEqualTo(abortAfterWrites);
        await Assert.That(ReferenceEquals(context.Response.Body, output)).IsTrue();
    }
}
