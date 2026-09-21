// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Buffers;
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Renders fragments with <see cref="MarkdownRenderer"/>.</summary>
internal static class OursRenderer
{
    /// <summary>Renders Markdown to HTML, turning any exception into an error result.</summary>
    /// <param name="markdown">Markdown source.</param>
    /// <returns>The HTML, or the exception description.</returns>
    internal static RenderResult Render(string markdown)
    {
        try
        {
            var writer = new ArrayBufferWriter<byte>();
            MarkdownRenderer.Render(Encoding.UTF8.GetBytes(markdown), writer);
            return new(Encoding.UTF8.GetString(writer.WrittenSpan), null);
        }
        catch (Exception exception)
        {
            return new(null, $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
