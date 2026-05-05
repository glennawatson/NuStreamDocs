// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;

namespace NuStreamDocs.Serve;

/// <summary>
/// ASP.NET Core middleware that intercepts HTML responses and splices
/// the LiveReload <c>&lt;script&gt;</c> tag in just before
/// <c>&lt;/body&gt;</c>. Non-HTML responses pass through untouched.
/// </summary>
internal static class HtmlInjectionMiddleware
{
    /// <summary>Length above which we don't try to splice (large HTML pages get the script appended at end-of-stream instead).</summary>
    private const int ScanLimitBytes = 64 * 1024;

    /// <summary>Middleware entry point.</summary>
    /// <param name="ctx">Request context.</param>
    /// <param name="next">Next middleware delegate.</param>
    /// <returns>Async task.</returns>
    public static async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        var originalBody = ctx.Response.Body;
        await using MemoryStream buffer = new();
        ctx.Response.Body = buffer;
        try
        {
            await next(ctx).ConfigureAwait(false);
            buffer.Position = 0;
            await CopyToWithInjectionAsync(ctx, buffer, originalBody).ConfigureAwait(false);
        }
        finally
        {
            ctx.Response.Body = originalBody;
        }
    }

    /// <summary>Copies the buffered response to <paramref name="originalBody"/>, splicing the reload script when the response is HTML.</summary>
    /// <param name="ctx">Request context (for content-type inspection + content-length adjustment).</param>
    /// <param name="buffer">Buffered response body.</param>
    /// <param name="originalBody">Real outbound stream.</param>
    /// <returns>Async task.</returns>
    private static async Task CopyToWithInjectionAsync(HttpContext ctx, MemoryStream buffer, Stream originalBody)
    {
        if (!IsHtmlResponse(ctx))
        {
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
            return;
        }

        var span = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var idx = LocateInjectionOffset(span);
        if (idx < 0)
        {
            // Fall back to appending at end-of-stream when no </body> is found.
            await TryAdjustContentLengthAsync(ctx, originalBody, buffer.Length + DevServer.ReloadScriptMarker.Length).ConfigureAwait(false);
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
            await originalBody.WriteAsync(DevServer.ReloadScriptMemory).ConfigureAwait(false);
            return;
        }

        var newLength = buffer.Length + DevServer.ReloadScriptMarker.Length;
        await TryAdjustContentLengthAsync(ctx, originalBody, newLength).ConfigureAwait(false);
        await originalBody.WriteAsync(buffer.GetBuffer().AsMemory(0, idx)).ConfigureAwait(false);
        await originalBody.WriteAsync(DevServer.ReloadScriptMemory).ConfigureAwait(false);
        await originalBody.WriteAsync(buffer.GetBuffer().AsMemory(idx, (int)buffer.Length - idx)).ConfigureAwait(false);
    }

    /// <summary>Returns true when the buffered response is text/html (or unset and the URL ends in <c>.html</c>).</summary>
    /// <param name="ctx">Request context.</param>
    /// <returns>True for HTML.</returns>
    private static bool IsHtmlResponse(HttpContext ctx)
    {
        var contentType = ctx.Response.ContentType;
        if (contentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true)
        {
            return true;
        }

        if (contentType is not null)
        {
            return false;
        }

        var path = ctx.Request.Path.Value;
        return !string.IsNullOrEmpty(path)
            && (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || path.EndsWith('/'));
    }

    /// <summary>Finds the offset of <c>&lt;/body&gt;</c> in <paramref name="span"/>, or <c>-1</c> when absent.</summary>
    /// <param name="span">Buffered response.</param>
    /// <returns>Insertion offset or <c>-1</c>.</returns>
    private static int LocateInjectionOffset(ReadOnlySpan<byte> span)
    {
        var scanLength = span.Length > ScanLimitBytes ? ScanLimitBytes : span.Length;
        var slice = span[^scanLength..];
        var rel = slice.IndexOf(DevServer.BodyCloseMarker);
        return rel < 0 ? -1 : span.Length - scanLength + rel;
    }

    /// <summary>Updates <c>Content-Length</c> when the framework set one; ignored otherwise.</summary>
    /// <param name="ctx">Request context.</param>
    /// <param name="originalBody">Real outbound stream (unused here; reserved for future flush coordination).</param>
    /// <param name="newLength">New length to advertise.</param>
    /// <returns>Async task.</returns>
    private static Task TryAdjustContentLengthAsync(HttpContext ctx, Stream originalBody, long newLength)
    {
        _ = originalBody;
        if (ctx.Response.ContentLength is not null)
        {
            ctx.Response.ContentLength = newLength;
        }

        return Task.CompletedTask;
    }
}
