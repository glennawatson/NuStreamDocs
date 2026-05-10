// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Arithmatex.MathJax;

/// <summary>
/// Ships the MathJax 3 runtime to every rendered page so <c>ArithmatexPlugin</c> output is
/// typeset client-side.
/// </summary>
public sealed class MathJaxPlugin : IPlugin, IHeadExtraProvider
{
    /// <summary>Pre-encoded UTF-8 head fragment computed once at construction.</summary>
    private readonly byte[] _headExtraBytes;

    /// <summary>Initializes a new instance of the <see cref="MathJaxPlugin"/> class with default options.</summary>
    public MathJaxPlugin()
        : this(MathJaxOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MathJaxPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public MathJaxPlugin(in MathJaxOptions options) => _headExtraBytes = ComposeHead(options);

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "mathjax"u8;

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        writer.Write(_headExtraBytes);
    }

    /// <summary>Builds the inline-config + async-loader head fragment for the given options.</summary>
    /// <param name="options">Plugin options.</param>
    /// <returns>UTF-8 head fragment bytes.</returns>
    private static byte[] ComposeHead(in MathJaxOptions options)
    {
        var loader = options.LoaderUrl.IsEmpty ? MathJaxOptions.Default.LoaderUrl.Value : options.LoaderUrl.Value;
        var processClass = (options.ProcessHtmlClass.IsEmpty ? MathJaxOptions.Default.ProcessHtmlClass.Value : options.ProcessHtmlClass.Value) ?? string.Empty;
        var ignoreClass = (options.IgnoreHtmlClass.IsEmpty ? MathJaxOptions.Default.IgnoreHtmlClass.Value : options.IgnoreHtmlClass.Value) ?? string.Empty;

        // MathJax 3 reads `window.MathJax` at startup, so we set it before loading the bundle.
        // Inline math delimiters mirror Arithmatex's output: `\(…\)` for inline, `\[…\]` for
        // display. Escape only the chars that matter inside a single-quoted JS string literal
        // ('\\' for backslash, "'" for the quote itself).
        const string ConfigTemplate =
            @"<script>window.MathJax={{tex:{{inlineMath:[['\\(','\\)']],displayMath:[['\\[','\\]']]," +
            "processEscapes:true,processEnvironments:true}}," +
            "options:{{ignoreHtmlClass:'{0}',processHtmlClass:'{1}'}}}};</script>";
        const string LoaderTemplate = "<script src=\"{0}\" async></script>";

        var config = string.Format(
            CultureInfo.InvariantCulture,
            ConfigTemplate,
            EscapeJsString(ignoreClass),
            EscapeJsString(processClass));
        var loaderTag = string.Format(
            CultureInfo.InvariantCulture,
            LoaderTemplate,
            EscapeAttribute(loader));
        return Encoding.UTF8.GetBytes(config + loaderTag);
    }

    /// <summary>Escapes the four characters meaningful inside a single-quoted JS string literal: backslash and apostrophe.</summary>
    /// <param name="value">Source string.</param>
    /// <returns>Escaped form safe to drop between single quotes.</returns>
    private static string EscapeJsString(string value) =>
        value.Replace("\\", @"\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    /// <summary>Escapes the two characters meaningful inside a double-quoted HTML attribute value: ampersand and double-quote.</summary>
    /// <param name="value">Source URL/string.</param>
    /// <returns>Escaped form safe to drop between double quotes.</returns>
    private static string EscapeAttribute(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
}
