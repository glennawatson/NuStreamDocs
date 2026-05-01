// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Config;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>
/// Outer-layer fluent builder for assembling a documentation site.
/// </summary>
/// <remarks>
/// One of the few **instance-shaped** types in the library: it holds
/// genuine per-build configuration state. All inner-layer parser /
/// emitter / search work happens through <c>static</c> methods invoked
/// from <see cref="BuildAsync()"/>. Plugins are registered via the
/// generic <see cref="UsePlugin{TPlugin}"/> method (AOT-clean: a
/// direct <c>new TPlugin()</c> call, no reflection) or via
/// <c>Use{Plugin}</c> extension methods shipped by each plugin
/// assembly.
/// </remarks>
public sealed class DocBuilder
{
    /// <summary>Initial slot capacity for registered plugins.</summary>
    private const int InitialPluginCapacity = 16;

    /// <summary>Justification text for the S4018 suppression on <see cref="UsePlugin{TPlugin}"/>.</summary>
    private const string S4018Justification =
        "TPlugin is intentionally the only input — the AOT-clean registration pattern is " +
        "`builder.UsePlugin<TPlugin>()` which compiles down to a direct `new TPlugin()` " +
        "call. Adding a parameter would defeat the point.";

    /// <summary>Registered plugin instances, in registration order.</summary>
    private readonly List<IDocPlugin> _plugins = new(InitialPluginCapacity);

    /// <summary>Registered config readers, in registration order.</summary>
    private readonly List<IConfigReader> _configReaders = new(2);

    /// <summary>Configured include globs (forward-slashed, relative to the docs root).</summary>
    private readonly List<string> _includes = new(2);

    /// <summary>Configured exclude globs (forward-slashed, relative to the docs root).</summary>
    private readonly List<string> _excludes = new(4);

    /// <summary>Configured input docs directory; defaults to <c>./docs</c>.</summary>
    private string _inputRoot = "./docs";

    /// <summary>Configured output site directory; defaults to <c>./site</c>.</summary>
    private string _outputRoot = "./site";

    /// <summary>Optional logger threaded through the build pipeline.</summary>
    private ILogger? _logger;

    /// <summary>When true, pages emit as <c>foo/index.html</c> and link rewriting targets <c>foo/</c>; mirrors mkdocs' <c>use_directory_urls</c>.</summary>
    private bool _useDirectoryUrls;

    /// <summary>When true, pages whose frontmatter declares <c>draft: true</c> are still rendered; otherwise drafts are skipped.</summary>
    private bool _includeDrafts;

    /// <summary>Gets a value indicating whether the build is configured for the directory-URL output shape.</summary>
    public bool UseDirectoryUrlsEnabled => _useDirectoryUrls;

    /// <summary>Gets a value indicating whether <c>draft: true</c> pages are emitted.</summary>
    public bool IncludeDraftsEnabled => _includeDrafts;

    /// <summary>Enables the directory-URL output shape (<c>foo/index.html</c> + <c>foo/</c> links).</summary>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder UseDirectoryUrls()
    {
        _useDirectoryUrls = true;
        return this;
    }

    /// <summary>Sets the directory-URL output toggle.</summary>
    /// <param name="enabled">True for <c>foo/index.html</c>; false keeps flat <c>foo.html</c> output.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder UseDirectoryUrls(bool enabled)
    {
        _useDirectoryUrls = enabled;
        return this;
    }

    /// <summary>Enables emission of <c>draft: true</c> pages (off by default).</summary>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder IncludeDrafts()
    {
        _includeDrafts = true;
        return this;
    }

    /// <summary>Sets the include-drafts toggle.</summary>
    /// <param name="enabled">True to render draft pages; false to skip them.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder IncludeDrafts(bool enabled)
    {
        _includeDrafts = enabled;
        return this;
    }

    /// <summary>Sets the logger used by <see cref="BuildAsync()"/> and the build pipeline.</summary>
    /// <param name="logger">Logger to use; null disables diagnostics.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder WithLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        return this;
    }

    /// <summary>Sets the input docs directory.</summary>
    /// <param name="path">Path to the docs root.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder WithInput(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _inputRoot = path;
        return this;
    }

    /// <summary>Sets the output site directory.</summary>
    /// <param name="path">Path to the output root.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder WithOutput(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _outputRoot = path;
        return this;
    }

    /// <summary>Adds an include glob (forward-slashed, relative to the docs root).</summary>
    /// <param name="pattern">Glob pattern, e.g. <c>guide/**/*.md</c>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// When at least one include is registered, only paths matching one
    /// of the includes are processed. Paths still need to survive the
    /// configured excludes.
    /// </remarks>
    public DocBuilder Include(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        _includes.Add(pattern);
        return this;
    }

    /// <summary>Adds one or more include globs (forward-slashed, relative to the docs root).</summary>
    /// <param name="patterns">Glob patterns.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder Include(params ReadOnlySpan<string> patterns)
    {
        for (var i = 0; i < patterns.Length; i++)
        {
            ArgumentException.ThrowIfNullOrEmpty(patterns[i]);
            _includes.Add(patterns[i]);
        }

        return this;
    }

    /// <summary>Adds an exclude glob (forward-slashed, relative to the docs root).</summary>
    /// <param name="pattern">Glob pattern, e.g. <c>drafts/**</c>.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder Exclude(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        _excludes.Add(pattern);
        return this;
    }

    /// <summary>Adds one or more exclude globs (forward-slashed, relative to the docs root).</summary>
    /// <param name="patterns">Glob patterns.</param>
    /// <returns>This builder for chaining.</returns>
    public DocBuilder Exclude(params ReadOnlySpan<string> patterns)
    {
        for (var i = 0; i < patterns.Length; i++)
        {
            ArgumentException.ThrowIfNullOrEmpty(patterns[i]);
            _excludes.Add(patterns[i]);
        }

        return this;
    }

    /// <summary>
    /// Registers a plugin via its parameterless constructor.
    /// </summary>
    /// <typeparam name="TPlugin">Plugin type implementing <see cref="IDocPlugin"/>.</typeparam>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// The <c>new()</c> constraint keeps the registration path
    /// reflection-free and AOT-safe. Plugin assemblies typically wrap
    /// this in a <c>Use{Plugin}</c> extension method that also accepts
    /// an options record.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S4018:Generic methods should provide type parameters", Justification = S4018Justification)]
    public DocBuilder UsePlugin<TPlugin>()
        where TPlugin : IDocPlugin, new()
    {
        _plugins.Add(new TPlugin());
        return this;
    }

    /// <summary>
    /// Registers a pre-constructed plugin instance.
    /// </summary>
    /// <param name="plugin">Plugin to register.</param>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// Used by extension methods that need to capture options before
    /// the plugin is added (most plugins).
    /// </remarks>
    public DocBuilder UsePlugin(IDocPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _plugins.Add(plugin);
        return this;
    }

    /// <summary>
    /// Returns the existing plugin instance of <typeparamref name="TPlugin"/>
    /// if one is registered, otherwise constructs a fresh one, registers
    /// it, and returns it.
    /// </summary>
    /// <typeparam name="TPlugin">Plugin type implementing <see cref="IDocPlugin"/> with a parameterless constructor.</typeparam>
    /// <returns>The single registered instance of <typeparamref name="TPlugin"/>.</returns>
    /// <remarks>
    /// Used by accumulator-shaped extension methods (e.g. <c>AddExtraCss</c>)
    /// that fold every chained call onto one underlying plugin.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S4018:Generic methods should provide type parameters", Justification = S4018Justification)]
    public TPlugin GetOrAddPlugin<TPlugin>()
        where TPlugin : class, IDocPlugin, new()
    {
        for (var i = 0; i < _plugins.Count; i++)
        {
            if (_plugins[i] is TPlugin existing)
            {
                return existing;
            }
        }

        var fresh = new TPlugin();
        _plugins.Add(fresh);
        return fresh;
    }

    /// <summary>Registers an <see cref="IConfigReader"/> with the builder.</summary>
    /// <param name="reader">The reader to register.</param>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// Each format ships in its own assembly and is registered through
    /// the assembly's <c>Use{Format}Config()</c> extension. The first
    /// reader whose <see cref="IConfigReader.RecognisesExtension"/>
    /// returns true is used at config-load time.
    /// </remarks>
    public DocBuilder UseConfigReader(IConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _configReaders.Add(reader);
        return this;
    }

    /// <summary>Returns the first registered reader whose extension matches, or null.</summary>
    /// <param name="extension">File extension including the leading dot, lowercase.</param>
    /// <returns>The matching reader, or null when none recognises the extension.</returns>
    public IConfigReader? FindConfigReader(ReadOnlySpan<char> extension)
    {
        for (var i = 0; i < _configReaders.Count; i++)
        {
            if (_configReaders[i].RecognisesExtension(extension))
            {
                return _configReaders[i];
            }
        }

        return null;
    }

    /// <summary>Runs the configured build pipeline without cancellation support.</summary>
    /// <returns>The number of pages emitted.</returns>
    public Task<int> BuildAsync() => BuildAsync(CancellationToken.None);

    /// <summary>Runs the configured build pipeline with cancellation support.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of pages emitted.</returns>
    public Task<int> BuildAsync(in CancellationToken cancellationToken) =>
        BuildPipeline.RunAsync(
            _inputRoot,
            _outputRoot,
            [.. _plugins],
            new BuildPipelineOptions(BuildPathFilter(), _logger, _useDirectoryUrls, _includeDrafts),
            cancellationToken);

    /// <summary>
    /// Renders a single in-memory document via every registered plugin's
    /// page hook.
    /// </summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="html">Pre-allocated UTF-8 sink the renderer and plugins write into.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every plugin's per-page hook has run.</returns>
    public async Task RenderPageAsync(string relativePath, ReadOnlyMemory<byte> source, ArrayBufferWriter<byte> html, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(html);
        MarkdownRenderer.Render(source.Span, html);

        var context = new PluginRenderContext(relativePath, source, html);
        for (var i = 0; i < _plugins.Count; i++)
        {
            await _plugins[i].OnRenderPageAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Builds the configured <see cref="PathFilter"/>.</summary>
    /// <returns>The composed filter, or <see cref="PathFilter.Empty"/> when nothing was registered.</returns>
    internal PathFilter BuildPathFilter() =>
        _includes is [] && _excludes is []
            ? PathFilter.Empty
            : new([.. _includes], [.. _excludes]);
}
