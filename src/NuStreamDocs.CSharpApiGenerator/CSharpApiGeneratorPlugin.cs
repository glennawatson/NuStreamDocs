// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Plugin that runs the C# reference generator before page discovery.
/// </summary>
/// <remarks>
/// In <see cref="CSharpApiGeneratorMode.EmitMarkdown"/> mode the plugin generates the pages, hands
/// each one to the build pipeline as a <see cref="SyntheticPage"/>, and records a lightweight
/// <see cref="SyntheticNavEntry"/> (relative path only) per page so the nav plugin can mirror the
/// generated tree. Generation runs to completion inside <see cref="DiscoverAsync"/> — the nav plugin
/// runs later in the same phase and needs the full page list — so the page bodies are buffered until
/// render drains them; no intermediate <c>.md</c> files land on disk. In <see cref="CSharpApiGeneratorMode.Direct"/>
/// mode it stashes the merged catalog on <see cref="LastExtraction"/> without invoking an emitter.
/// </remarks>
public sealed class CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions options, ILogger logger)
    : IBuildDiscoverPlugin, ISyntheticNavProvider
{
    /// <summary>Forward-slash byte separating path segments in synthetic relative paths.</summary>
    private const byte SlashByte = (byte)'/';

    /// <summary>Configured options.</summary>
    private readonly CSharpApiGeneratorOptions _options = ValidateOptions(options);

    /// <summary>Optional logger handed to the pipeline.</summary>
    private readonly ILogger _logger = logger;

    /// <summary>Backing store for <see cref="SyntheticNavEntries"/>; populated synchronously in <see cref="DiscoverAsync"/>.</summary>
    private SyntheticNavEntry[] _navEntries = [];

    /// <summary>Initializes a new instance of the <see cref="CSharpApiGeneratorPlugin"/> class.</summary>
    /// <param name="options">Generator options.</param>
    public CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Gets the most recent direct-extract result, or <c>null</c> when the plugin ran in <see cref="CSharpApiGeneratorMode.EmitMarkdown"/> or has not yet run.</summary>
    public DirectExtractionResult? LastExtraction { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<SyntheticNavEntry> SyntheticNavEntries => _navEntries;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "csharp-apigenerator"u8;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Earliest);

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        if (_options.Mode is CSharpApiGeneratorMode.Direct)
        {
            await RunDirectAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // EmitMarkdown mode: generation runs to completion here (not on a background task) because
        // the nav plugin runs later in this same phase and needs the full page list. The page bodies
        // pile up in the unbounded channel until render drains them — the trade we accept for a
        // complete nav tree without spilling intermediate .md files onto disk. We retain only the
        // lightweight per-page nav metadata (the relative path); titles fall back to the path stem.
        var subdir = _options.OutputMarkdownSubdirectory;
        var channel = Channel.CreateUnbounded<SyntheticPage>(new()
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        // Pre-seed the landing-page nav entry so a totally-empty generation (or a generation that
        // throws) still leaves the "API" section pointed somewhere sensible. The "API Reference"
        // fallback mirrors ApiIndexWriter's default heading.
        var navEntries = new ConcurrentBag<SyntheticNavEntry>();
        if (_options.EmitIndexPage)
        {
            var indexTitle = _options.IndexTitle is { Length: > 0 } configured
                ? configured
                : "API Reference"u8.ToArray();
            navEntries.Add(
                new(BuildVirtualPath(subdir, "index.md"u8.ToArray()), indexTitle, _options.IndexOrder, false));
        }

        _navEntries = [.. navEntries];

        ConcurrentDictionary<byte[], byte> namespaces = new(ByteArrayComparer.Instance);
        var sink = new CallbackPageSink((relativePath, bytes) =>
        {
            // Track the first path segment for the index page; relativePath comes from the
            // SourceDocParser emitter as a forward-slashed string. Keep the conversion to
            // UTF-8 bytes to a single Encoding pass at this boundary — everything else
            // downstream stays byte-shaped.
            var relBytes = Encoding.UTF8.GetBytes(relativePath);
            var slashIdx = Array.IndexOf(relBytes, SlashByte);
            if (slashIdx > 0)
            {
                var head = relBytes.AsSpan(0, slashIdx);
                if (!ApiIndexWriter.IsInfraDirectory(head))
                {
                    namespaces.TryAdd(head.ToArray(), 0);
                }
            }

            var virtualPath = BuildVirtualPath(subdir, relBytes);
            channel.Writer.TryWrite(new(virtualPath, bytes));

            // Path-only entry: the grafter derives section titles from directory names and page
            // titles from file stems — clean for the vast majority of API pages — so we don't
            // retain a title byte[] per page.
            navEntries.Add(new(virtualPath, null, null, false));
        });

        try
        {
            await CSharpApiGenerator.GenerateAsync(_options, sink, _logger, cancellationToken).ConfigureAwait(false);
            EmitIndexPageIfRequested(channel.Writer, subdir, namespaces);
        }
        finally
        {
            channel.Writer.Complete();
            _navEntries = [.. navEntries];
        }

        context.SyntheticPages.RegisterStream(StreamFromChannelAsync(channel.Reader, cancellationToken));
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static CSharpApiGeneratorOptions ValidateOptions(CSharpApiGeneratorOptions opts)
    {
        opts.Validate();
        return opts;
    }

    /// <summary>
    /// Builds the synthetic page's relative path: <c>{subdir}/{emitterRelative}</c> via the
    /// canonical <see cref="DirectoryPath.UrlJoin"/> helper so the join always uses forward
    /// slashes regardless of host OS.
    /// </summary>
    /// <param name="subdir">Configured output subdirectory (PathSegment, e.g. <c>api</c>).</param>
    /// <param name="emitterRelative">UTF-8 bytes of the per-page relative path the emitter produced.</param>
    /// <returns>Forward-slashed virtual path under <c>InputRoot</c>.</returns>
    private static FilePath BuildVirtualPath(in PathSegment subdir, byte[] emitterRelative)
    {
        // Single UTF-8 → string decode at the BCL boundary; UrlJoin then composes with
        // explicit '/' separators (no Path.Combine, so Windows / Linux match).
        var emitterRelativePath = (UrlPath)Encoding.UTF8.GetString(emitterRelative);
        if (subdir.IsEmpty)
        {
            return (FilePath)emitterRelativePath.Value;
        }

        return DirectoryPath.FromString(subdir).UrlJoin(emitterRelativePath);
    }

    /// <summary>Drains <paramref name="reader"/>; the channel is already fully written and completed by the time render enumerates this.</summary>
    /// <param name="reader">Channel reader the sink wrote pages into during <see cref="DiscoverAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token observed between pages.</param>
    /// <returns>The async stream of synthetic pages handed to the pipeline.</returns>
    private static async IAsyncEnumerable<SyntheticPage> StreamFromChannelAsync(
        ChannelReader<SyntheticPage> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var page in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return page;
        }
    }

    /// <summary>Runs <see cref="CSharpApiGenerator.ExtractAsync"/> and stashes the result on <see cref="LastExtraction"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes once the catalog has been merged.</returns>
    private async Task RunDirectAsync(CancellationToken cancellationToken) =>
        LastExtraction = await CSharpApiGenerator.ExtractAsync(_options, _logger, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Builds and pushes the optional <c>{subdir}/index.md</c> landing page after the per-type pages have streamed.</summary>
    /// <param name="writer">Channel writer the index page is enqueued on.</param>
    /// <param name="subdir">Configured output subdirectory.</param>
    /// <param name="namespaces">Distinct namespace folder names captured from the emitter callback.</param>
    private void EmitIndexPageIfRequested(
        ChannelWriter<SyntheticPage> writer,
        in PathSegment subdir,
        ConcurrentDictionary<byte[], byte> namespaces)
    {
        if (!_options.EmitIndexPage || namespaces.IsEmpty)
        {
            return;
        }

        var sorted = new byte[namespaces.Count][];
        var i = 0;
        foreach (var key in namespaces.Keys)
        {
            sorted[i++] = key;
        }

        Array.Sort(sorted, ByteArrayComparer.Instance);
        var indexBytes = ApiIndexWriter.BuildBytes(
            sorted,
            (_options.IndexTitle ?? []).AsSpan(),
            (_options.IndexIntroduction ?? []).AsSpan(),
            _options.IndexOrder);
        if (indexBytes.Length is 0)
        {
            return;
        }

        writer.TryWrite(new(BuildVirtualPath(subdir, "index.md"u8.ToArray()), indexBytes));
    }
}
