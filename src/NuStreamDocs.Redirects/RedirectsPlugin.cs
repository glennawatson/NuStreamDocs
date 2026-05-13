// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Links;
using NuStreamDocs.Plugins;
using NuStreamDocs.Redirects.Logging;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Redirects;

/// <summary>Emits the <c>_redirects</c> file, per-redirect meta-refresh HTML pages, and the <c>_headers</c> file at the end of the build.</summary>
public sealed class RedirectsPlugin : IBuildConfigurePlugin, IPageScanPlugin, IBuildFinalizePlugin
{
    /// <summary>Byte separators that delimit entries inside a <c>redirect_from</c> frontmatter value (whitespace, commas, flow-list brackets, quotes).</summary>
    private static readonly byte[] FrontmatterSeparators =
        [(byte)' ', (byte)'\t', (byte)'\r', (byte)'\n', (byte)',', (byte)'[', (byte)']', (byte)'"', (byte)'\''];

    /// <summary>Plugin options.</summary>
    private readonly RedirectsOptions _options;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Redirects collected from page frontmatter during the scan phase.</summary>
    private readonly ConcurrentBag<RedirectRule> _scanned = [];

    /// <summary>Whether the site emits directory-style URLs (captured during configure).</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class with default options.</summary>
    public RedirectsPlugin()
        : this(RedirectsOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public RedirectsPlugin(in RedirectsOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public RedirectsPlugin(in RedirectsOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "redirects"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ScanPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _useDirectoryUrls = context.UseDirectoryUrls;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Scan(in PageScanContext context)
    {
        if (!_options.ReadFrontmatterRedirects)
        {
            return;
        }

        ArrayBufferWriter<byte> values = new();
        FrontmatterValueExtractor.AppendKeysTo(context.Source, [_options.FrontmatterKey], values);
        var raw = values.WrittenSpan;
        if (raw.IsEmpty)
        {
            return;
        }

        var pagePath = _useDirectoryUrls ? context.RelativePath : context.RelativePath.WithExtension(".html");
        var target = ServedUrlBytes.FromPath(pagePath, _useDirectoryUrls, true);
        var start = 0;
        for (var i = 0; i <= raw.Length; i++)
        {
            var atSeparator = i == raw.Length || Array.IndexOf(FrontmatterSeparators, raw[i]) >= 0;
            if (!atSeparator)
            {
                continue;
            }

            if (i > start)
            {
                _scanned.Add(new(raw[start..i].ToArray(), target, true));
            }

            start = i + 1;
        }
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        if (context.OutputRoot.IsEmpty)
        {
            return;
        }

        var redirects = MergeRedirects();
        var headerRules = BuildHeaderRules();

        if (_options.EmitRedirectsFile && redirects is [_, ..])
        {
            await WriteFileAsync(
                context.OutputRoot,
                "_redirects",
                w => RedirectFileWriter.WriteRedirectsFile(redirects, w),
                cancellationToken).ConfigureAwait(false);
        }

        if (_options.EmitMetaRefreshPages)
        {
            await WriteMetaRefreshPagesAsync(context.OutputRoot, redirects, cancellationToken).ConfigureAwait(false);
        }

        if (_options.EmitHeadersFile && headerRules is [_, ..])
        {
            await WriteFileAsync(
                context.OutputRoot,
                "_headers",
                w => HeadersFileWriter.WriteHeadersFile(headerRules, w),
                cancellationToken).ConfigureAwait(false);
        }

        RedirectsLogging.LogWritten(_logger, redirects.Count, headerRules.Count);
    }

    /// <summary>Writes a file under <paramref name="outputRoot"/> from content produced into a buffer.</summary>
    /// <param name="outputRoot">Site output directory.</param>
    /// <param name="fileName">File name relative to the output root.</param>
    /// <param name="write">Callback that writes the file content into the supplied buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    private static async Task WriteFileAsync(
        DirectoryPath outputRoot,
        string fileName,
        Action<ArrayBufferWriter<byte>> write,
        CancellationToken cancellationToken)
    {
        ArrayBufferWriter<byte> sink = new();
        write(sink);
        await File.WriteAllBytesAsync(
            Path.Combine(outputRoot.Value, fileName),
            sink.WrittenSpan.ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Merges the configured redirects with the ones scanned from frontmatter; configured entries win on a duplicate source.</summary>
    /// <returns>The merged redirect list.</returns>
    private List<RedirectRule> MergeRedirects()
    {
        List<RedirectRule> merged = new(_options.Redirects.Length + _scanned.Count);
        HashSet<byte[]> seen = new(ByteArrayComparer.Instance);
        for (var i = 0; i < _options.Redirects.Length; i++)
        {
            var rule = _options.Redirects[i];
            if (seen.Add(rule.From))
            {
                merged.Add(rule);
            }
        }

        foreach (var rule in _scanned)
        {
            if (seen.Add(rule.From))
            {
                merged.Add(rule);
            }
        }

        return merged;
    }

    /// <summary>Builds the <c>_headers</c> rule list: the default cache rules (when enabled) followed by the author's rules.</summary>
    /// <returns>The combined rule list.</returns>
    private List<HeaderRule> BuildHeaderRules()
    {
        List<HeaderRule> rules = [];
        if (_options.DefaultCacheHeaders)
        {
            rules.AddRange(HeadersFileWriter.DefaultRules());
        }

        rules.AddRange(_options.Headers);
        return rules;
    }

    /// <summary>Writes a meta-refresh HTML page for each redirect whose source path isn't already a rendered page.</summary>
    /// <param name="outputRoot">Site output directory.</param>
    /// <param name="redirects">The merged redirects.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when all pages have been written.</returns>
    private async Task WriteMetaRefreshPagesAsync(
        DirectoryPath outputRoot,
        List<RedirectRule> redirects,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < redirects.Count; i++)
        {
            var rule = redirects[i];
            if (rule.From is not [_, ..] || rule.To is not [_, ..])
            {
                RedirectsLogging.LogIgnoredRedirect(
                    _logger,
                    rule.From is [_, ..] ? Encoding.UTF8.GetString(rule.From) : "<empty>");
                continue;
            }

            var fromPath = Encoding.UTF8.GetString(rule.From);
            var relativeFile = SourceToRelativeFile(fromPath);
            var targetFile = Path.Combine(outputRoot.Value, relativeFile);
            if (File.Exists(targetFile))
            {
                RedirectsLogging.LogSkippedClobber(_logger, fromPath);
                continue;
            }

            var dir = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            ArrayBufferWriter<byte> sink = new();
            RedirectFileWriter.WriteMetaRefreshHtml(rule.To, sink);
            await File.WriteAllBytesAsync(targetFile, sink.WrittenSpan.ToArray(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Maps a redirect-source URL path to the output file that holds its meta-refresh page.</summary>
    /// <param name="fromPath">The redirect source (a root-relative URL path).</param>
    /// <returns>A relative file path, using forward slashes.</returns>
    private string SourceToRelativeFile(string fromPath)
    {
        var rel = fromPath.StartsWith('/') ? fromPath[1..] : fromPath;
        if (rel.Length is 0)
        {
            return "index.html";
        }

        if (rel.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            rel.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
        {
            return rel;
        }

        if (rel.EndsWith('/'))
        {
            return rel + "index.html";
        }

        return rel + (_useDirectoryUrls ? "/index.html" : ".html");
    }
}
