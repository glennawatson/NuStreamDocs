// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Glossary;

/// <summary>Synthesizes a central glossary Markdown page and wraps matching prose terms in accessible <c>&lt;abbr&gt;</c> elements.</summary>
[System.Diagnostics.DebuggerDisplay("GlossaryPlugin: {Name}")]
public sealed class GlossaryPlugin
    : IBuildConfigurePlugin,
        IBuildDiscoverPlugin,
        IPagePostRenderPlugin,
        IHeadExtraProvider,
        IStaticAssetProvider
{
    /// <summary>Initial buffer capacity for the generated glossary Markdown page.</summary>
    private const int InitialPageCapacity = 1024;

    /// <summary>Post-render offset within the Normal priority band.</summary>
    private const int PostRenderOffset = 60;

    /// <summary>Site-relative path for the glossary stylesheet.</summary>
    private static readonly FilePath StylesheetPath = new("assets/stylesheets/nstd-glossary.css");

    /// <summary>Configured options.</summary>
    private readonly GlossaryOptions _options;

    /// <summary>Longest-first entries used during HTML post-rendering.</summary>
    private GlossaryEntry[] _longestFirst = [];

    /// <summary>Served base URL of the glossary page (such as <c>/glossary/</c> or <c>/glossary/index.html</c>).</summary>
    private byte[] _glossaryBaseUrl = [];

    /// <summary>Relative path of the synthetic glossary Markdown page so it does not auto-link itself.</summary>
    private FilePath _glossaryMarkdownPath;

    /// <summary>Initializes a new instance of the <see cref="GlossaryPlugin"/> class with default options.</summary>
    public GlossaryPlugin()
        : this(GlossaryOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GlossaryPlugin"/> class.</summary>
    /// <param name="options">Glossary plugin options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public GlossaryPlugin(GlossaryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        StaticAssets = options.AutoLinkTerms
            ? [(StylesheetPath, GlossaryStylesheet.Bytes.ToArray())]
            : [];
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "glossary"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Early);

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Normal, PostRenderOffset);

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets { get; }

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var subdir = _options.OutputSubdirectory.IsEmpty
            ? string.Empty
            : _options.OutputSubdirectory.Value.Trim('/');
        _glossaryMarkdownPath = string.IsNullOrEmpty(subdir) ? new("index.md") : new($"{subdir}/index.md");
        _glossaryBaseUrl = _options.GenerateGlossaryPage
            ? BuildGlossaryBaseUrl(subdir, context.UseDirectoryUrls)
            : [];
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        var (sorted, longestFirst) = await GlossaryLoader.LoadAsync(_options, context.InputRoot, cancellationToken)
            .ConfigureAwait(false);
        _longestFirst = longestFirst;
        if (!_options.GenerateGlossaryPage || sorted.Length == 0)
        {
            return;
        }

        ArrayBufferWriter<byte> writer = new(InitialPageCapacity);
        GlossaryPageWriter.WriteMarkdown(writer, _options.PageTitle, sorted);
        context.SyntheticPages.Add(new(_glossaryMarkdownPath, writer.WrittenSpan.ToArray()));
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        _options.AutoLinkTerms && GlossaryHtmlRewriter.NeedsRewrite(html, _longestFirst);

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        if (string.Equals(context.RelativePath.Value, _glossaryMarkdownPath.Value, StringComparison.OrdinalIgnoreCase))
        {
            context.Output.Write(context.Html);
            return;
        }

        GlossaryHtmlRewriter.Rewrite(
            context.Html,
            _longestFirst,
            _glossaryBaseUrl,
            _options.FirstOccurrenceOnly,
            context.Output);
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        if (!_options.AutoLinkTerms)
        {
            return;
        }

        writer.Write("<link rel=\"stylesheet\" href=\"/assets/stylesheets/nstd-glossary.css\">\n"u8);
    }

    /// <summary>Builds the served glossary base URL bytes for <paramref name="subdir"/>.</summary>
    /// <param name="subdir">Trimmed subdirectory or empty.</param>
    /// <param name="useDirectoryUrls">Whether directory URLs are enabled.</param>
    /// <returns>UTF-8 base URL bytes.</returns>
    private static byte[] BuildGlossaryBaseUrl(string subdir, bool useDirectoryUrls)
    {
        if (useDirectoryUrls)
        {
            return Encoding.UTF8.GetBytes(string.IsNullOrEmpty(subdir) ? "/" : $"/{subdir}/");
        }

        return Encoding.UTF8.GetBytes(string.IsNullOrEmpty(subdir) ? "/index.html" : $"/{subdir}/index.html");
    }
}
