// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Transitions;

/// <summary>Ships the client router script and the head-extra (config meta + script tag) that turn a NuStreamDocs site into an instant-navigation, view-transitioned SPA.</summary>
public sealed class TransitionsPlugin : IPlugin, IHeadExtraProvider, IStaticAssetProvider
{
    /// <summary>Maximum digits the <c>delay</c> value needs when formatted.</summary>
    private const int DelayBufferSize = 12;

    /// <summary>Site-relative path the router script is written to.</summary>
    private static readonly FilePath RouterScriptPath = new("assets/javascripts/nstd-router.js");

    /// <summary>Head-extra bytes computed from the options.</summary>
    private readonly byte[] _headExtra;

    /// <summary>Initializes a new instance of the <see cref="TransitionsPlugin"/> class with default options.</summary>
    public TransitionsPlugin()
        : this(TransitionsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TransitionsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public TransitionsPlugin(in TransitionsOptions options)
    {
        if (options.Enabled)
        {
            _headExtra = BuildHeadExtra(options);
            StaticAssets = [(RouterScriptPath, RouterScript.Bytes.ToArray())];
        }
        else
        {
            _headExtra = [];
            StaticAssets = [];
        }
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "transitions"u8;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets { get; }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer) => writer.Write(_headExtra);

    /// <summary>Builds the head-extra bytes: the <c>nstd:router</c> config meta tag followed by the deferred router script tag.</summary>
    /// <param name="options">Plugin options.</param>
    /// <returns>The head-extra bytes.</returns>
    private static byte[] BuildHeadExtra(in TransitionsOptions options)
    {
        ArrayBufferWriter<byte> sink = new();
        sink.Write("<meta name=\"nstd:router\" content=\"content="u8);
        WriteAttributeEscaped(sink, options.ContentSelector);
        sink.Write(";nav="u8);
        WriteAttributeEscaped(sink, options.NavSelector);
        sink.Write(";prefetch="u8);
        sink.Write(PrefetchToken(options.Prefetch));
        sink.Write(";delay="u8);
        WriteInt(sink, options.PrefetchDelayMs);
        sink.Write(";animation="u8);
        sink.Write(AnimationToken(options.Animation));
        sink.Write(";ignore="u8);
        WriteAttributeEscaped(sink, options.IgnoreSelector);
        sink.Write("\">\n<script src=\"/assets/javascripts/nstd-router.js\" defer></script>\n"u8);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Writes <paramref name="value"/> into an HTML double-quoted attribute, escaping <c>&amp;</c> and <c>"</c>.</summary>
    /// <param name="sink">Destination.</param>
    /// <param name="value">UTF-8 bytes to write.</param>
    private static void WriteAttributeEscaped(IBufferWriter<byte> sink, byte[] value)
    {
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if (b is not ((byte)'&' or (byte)'"'))
            {
                continue;
            }

            sink.Write(value.AsSpan(start, i - start));
            sink.Write(b == (byte)'&' ? "&amp;"u8 : "&quot;"u8);
            start = i + 1;
        }

        sink.Write(value.AsSpan(start));
    }

    /// <summary>Writes <paramref name="value"/> as decimal digits.</summary>
    /// <param name="sink">Destination.</param>
    /// <param name="value">The integer to write.</param>
    private static void WriteInt(IBufferWriter<byte> sink, int value)
    {
        Span<byte> buffer = stackalloc byte[DelayBufferSize];
        if (!Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            return;
        }

        sink.Write(buffer[..written]);
    }

    /// <summary>Maps a <see cref="PrefetchStrategy"/> to its config token.</summary>
    /// <param name="prefetch">The strategy.</param>
    /// <returns>The token bytes.</returns>
    private static ReadOnlySpan<byte> PrefetchToken(PrefetchStrategy prefetch) => prefetch switch
    {
        PrefetchStrategy.Viewport => "viewport"u8,
        PrefetchStrategy.Off => "off"u8,
        _ => "hover"u8,
    };

    /// <summary>Maps a <see cref="TransitionAnimation"/> to its config token.</summary>
    /// <param name="animation">The animation.</param>
    /// <returns>The token bytes.</returns>
    private static ReadOnlySpan<byte> AnimationToken(TransitionAnimation animation) => animation switch
    {
        TransitionAnimation.None => "none"u8,
        _ => "fade"u8,
    };
}
