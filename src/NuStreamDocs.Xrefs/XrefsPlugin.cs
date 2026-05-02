// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Autorefs;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Xrefs;

/// <summary>
/// DocFX-style xrefmap plugin.
/// </summary>
/// <remarks>
/// <para>
/// At <see cref="OnConfigureAsync"/> the plugin pulls every
/// configured <see cref="XrefImport"/> and registers each
/// <c>(uid, href)</c> pair into the shared
/// <see cref="AutorefsRegistry"/> with the import's base URL
/// prepended — so cross-site UIDs resolve to absolute URLs through
/// the same <c>@autoref:</c> rewrite path the local registry uses.
/// </para>
/// <para>
/// At <see cref="OnFinalizeAsync"/> the plugin snapshots the same
/// registry and writes <c>xrefmap.json</c> at the site root. Because
/// imports were registered with their absolute base URLs, downstream
/// consumers of the emitted map see only this site's contributions
/// when filtered by base URL.
/// </para>
/// </remarks>
public sealed class XrefsPlugin : IDocPlugin
{
    /// <summary>Long-lived <see cref="HttpClient"/> shared across import fetches; avoids socket exhaustion that a per-call <c>new HttpClient()</c> would cause.</summary>
    private static readonly HttpClient SharedClient = new();

    /// <summary>Configured options.</summary>
    private readonly XrefsOptions _options;

    /// <summary>Initializes a new instance of the <see cref="XrefsPlugin"/> class with default options and a fresh registry.</summary>
    public XrefsPlugin()
        : this(new(), XrefsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="XrefsPlugin"/> class with default options.</summary>
    /// <param name="registry">Shared autorefs registry.</param>
    public XrefsPlugin(AutorefsRegistry registry)
        : this(registry, XrefsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="XrefsPlugin"/> class.</summary>
    /// <param name="registry">Shared autorefs registry.</param>
    /// <param name="options">Plugin options.</param>
    public XrefsPlugin(AutorefsRegistry registry, XrefsOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        Registry = registry;
        _options = options;
    }

    /// <summary>Gets the shared registry the plugin reads from and writes into.</summary>
    public AutorefsRegistry Registry { get; }

    /// <inheritdoc/>
    public string Name => "xrefs";

    /// <inheritdoc/>
    public async ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        if (_options.Imports.Length is 0)
        {
            return;
        }

        for (var i = 0; i < _options.Imports.Length; i++)
        {
            await ImportOneAsync(_options.Imports[i], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_options.EmitMap)
        {
            return ValueTask.CompletedTask;
        }

        Directory.CreateDirectory(context.OutputRoot);
        var outputPath = Path.Combine(context.OutputRoot, _options.OutputFileName);
        XrefMapWriter.Write(outputPath, _options.BaseUrl, Registry.Snapshot());
        return ValueTask.CompletedTask;
    }

    /// <summary>Resolves the effective URL prefix for an import — option override beats embedded <c>baseUrl</c>.</summary>
    /// <param name="optionOverride">Caller-supplied base URL.</param>
    /// <param name="embedded">Embedded <c>baseUrl</c> from the imported file.</param>
    /// <returns>The effective prefix; empty when neither was set.</returns>
    private static string ResolvePrefix(string optionOverride, string embedded) =>
        !string.IsNullOrEmpty(optionOverride) ? optionOverride : embedded;

    /// <summary>Joins <paramref name="prefix"/> and <paramref name="suffix"/> with one separating <c>/</c>; passes through when prefix is empty.</summary>
    /// <param name="prefix">Base URL or path prefix.</param>
    /// <param name="suffix">Relative href.</param>
    /// <returns>Combined URL.</returns>
    private static string Combine(string prefix, string suffix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return suffix;
        }

        if (prefix.EndsWith('/') || suffix.StartsWith('/'))
        {
            return prefix + suffix;
        }

        return prefix + "/" + suffix;
    }

    /// <summary>Reads <paramref name="source"/> as a local file (when it exists) or fetches it as <c>http(s)://</c>.</summary>
    /// <param name="source">Local path or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document bytes; empty on miss or transport error.</returns>
    private static async Task<byte[]> ReadImportBytesAsync(string source, CancellationToken cancellationToken)
    {
        if (File.Exists(source))
        {
            return await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return [];
        }

        try
        {
            return await SharedClient.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
    }

    /// <summary>Fetches one import (local file or remote URL) and registers every entry into <see cref="Registry"/>.</summary>
    /// <param name="import">Import to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task tracking the fetch + register work.</returns>
    private async Task ImportOneAsync(XrefImport import, CancellationToken cancellationToken)
    {
        var bytes = await ReadImportBytesAsync(import.Source, cancellationToken).ConfigureAwait(false);
        if (bytes.Length is 0)
        {
            return;
        }

        var payload = XrefMapReader.Read(bytes);
        var prefix = ResolvePrefix(import.BaseUrl, payload.BaseUrl);
        for (var i = 0; i < payload.Entries.Length; i++)
        {
            var (uid, href) = payload.Entries[i];
            Registry.Register(uid, Combine(prefix, href), fragment: null);
        }
    }
}
