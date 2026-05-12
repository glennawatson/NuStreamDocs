// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Csp;

/// <summary>Injects a per-page <c>&lt;meta http-equiv="Content-Security-Policy"&gt;</c> into the head, hashing each page's inline scripts (and, optionally, styles) into the policy.</summary>
public sealed class CspPlugin : IPagePostRenderPlugin
{
    /// <summary>Tiebreak putting this after the privacy plugin's external-asset rewrite, so the policy needn't allow rewritten-away origins.</summary>
    private const int PostRenderTiebreak = 100;

    /// <summary>The closing-head marker the <c>&lt;meta&gt;</c> is spliced in front of.</summary>
    private static readonly byte[] HeadClose = [.. "</head>"u8];

    /// <summary>Plugin options.</summary>
    private readonly CspOptions _options;

    /// <summary>Initializes a new instance of the <see cref="CspPlugin"/> class with default options.</summary>
    public CspPlugin()
        : this(CspOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CspPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public CspPlugin(in CspOptions options) => _options = options;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "csp"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => _options.Enabled && html.IndexOf(HeadClose) >= 0;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var html = context.Html;
        var headClose = html.IndexOf(HeadClose);
        if (!_options.Enabled || headClose < 0)
        {
            context.Output.Write(html);
            return;
        }

        List<byte[]> scriptHashes = [];
        List<byte[]> styleHashes = [];
        if ((_options.HashInlineScripts || _options.HashInlineStyles) && CspInlineHasher.MayHaveInlineBlocks(html))
        {
            if (_options.HashInlineScripts)
            {
                CspInlineHasher.HashScripts(html, scriptHashes);
            }

            if (_options.HashInlineStyles)
            {
                CspInlineHasher.HashStyles(html, styleHashes);
            }
        }

        var csp = CspBuilder.Build(scriptHashes, styleHashes, _options);
        context.Output.Write(html[..headClose]);
        context.Output.Write("<meta http-equiv=\""u8);
        context.Output.Write(_options.Mode == CspMode.ReportOnly ? "Content-Security-Policy-Report-Only"u8 : "Content-Security-Policy"u8);
        context.Output.Write("\" content=\""u8);
        WriteAttributeEscaped(context.Output, csp);
        context.Output.Write("\">\n"u8);
        context.Output.Write(html[headClose..]);
    }

    /// <summary>Writes <paramref name="value"/> into an HTML double-quoted attribute, escaping <c>&amp;</c> and <c>"</c>.</summary>
    /// <param name="sink">Destination.</param>
    /// <param name="value">UTF-8 bytes to write.</param>
    private static void WriteAttributeEscaped(IBufferWriter<byte> sink, byte[] value)
    {
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is not ((byte)'&' or (byte)'"'))
            {
                continue;
            }

            sink.Write(value.AsSpan(start, i - start));
            sink.Write(value[i] == (byte)'&' ? "&amp;"u8 : "&quot;"u8);
            start = i + 1;
        }

        sink.Write(value.AsSpan(start));
    }
}
