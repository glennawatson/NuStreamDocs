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
/// In <see cref="CSharpApiGeneratorMode.EmitMarkdown"/> mode the plugin streams generated
/// pages straight into the build pipeline as <see cref="SyntheticPage"/>s — no intermediate
/// <c>.md</c> files land on disk, so the source tree stays clean even on packages that emit
/// thousands of API pages. In <see cref="CSharpApiGeneratorMode.Direct"/> mode it stashes
/// the merged catalog on <see cref="LastExtraction"/> without invoking an emitter.
/// </remarks>
public sealed class CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions options, ILogger logger) : IBuildDiscoverPlugin, ISyntheticNavProvider
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
    public ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        if (_options.Mode is CSharpApiGeneratorMode.Direct)
        {
            return new(RunDirectAsync(cancellationToken));
        }

        // EmitMarkdown mode: build a Channel-backed sink, kick off the SourceDocParser
        // run in the background, and register the channel reader as a SyntheticPage stream.
        // The build pipeline pulls one page at a time so peak memory tracks the in-flight
        // page rather than the full catalog.
        var subdir = _options.OutputMarkdownSubdirectory;

        // Publish the landing page's nav metadata synchronously — its title/order come
        // straight from the options, no generation needed — so the nav plugin (runs later
        // in the discover phase, can't see synthetic pages) can graft an "API" section
        // without us holding any generated page bodies for it. The "API Reference" fallback
        // mirrors ApiIndexWriter's default heading.
        if (_options.EmitIndexPage)
        {
            var indexTitle = _options.IndexTitle is { Length: > 0 } configured ? configured : "API Reference"u8.ToArray();
            _navEntries = [new SyntheticNavEntry(BuildVirtualPath(subdir, "index.md"u8.ToArray()), indexTitle, _options.IndexOrder, Hidden: false)];
        }

        var channel = Channel.CreateUnbounded<SyntheticPage>(new()
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

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

            channel.Writer.TryWrite(new(BuildVirtualPath(subdir, relBytes), bytes));
        });

        var generation = Task.Run(
            async () =>
            {
                try
                {
                    await CSharpApiGenerator.GenerateAsync(_options, sink, _logger, cancellationToken).ConfigureAwait(false);
                    EmitIndexPageIfRequested(channel.Writer, subdir, namespaces);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            },
            cancellationToken);

        context.SyntheticPages.RegisterStream(StreamFromChannelAsync(channel.Reader, generation, cancellationToken));
        return ValueTask.CompletedTask;
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

    /// <summary>Drains <paramref name="reader"/>, surfacing any background-task exception once the channel completes.</summary>
    /// <param name="reader">Channel reader the sink writes pages into.</param>
    /// <param name="generation">Background extraction task; awaited after the channel closes so its exception (if any) reaches the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token observed between pages.</param>
    /// <returns>The async stream of synthetic pages handed to the pipeline.</returns>
    private static async IAsyncEnumerable<SyntheticPage> StreamFromChannelAsync(
        ChannelReader<SyntheticPage> reader,
        Task generation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var page in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return page;
        }

        await generation.ConfigureAwait(false);
    }

    /// <summary>Runs <see cref="CSharpApiGenerator.ExtractAsync"/> and stashes the result on <see cref="LastExtraction"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes once the catalog has been merged.</returns>
    private async Task RunDirectAsync(CancellationToken cancellationToken) =>
        LastExtraction = await CSharpApiGenerator.ExtractAsync(_options, _logger, cancellationToken).ConfigureAwait(false);

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
