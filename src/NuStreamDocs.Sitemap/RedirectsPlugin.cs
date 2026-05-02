// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Plugins;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Sitemap;

/// <summary>
/// Emits meta-refresh HTML stubs at <c>from</c> paths that point
/// at <c>to</c> URLs — the static-site equivalent of an HTTP
/// redirect.
/// </summary>
/// <remarks>
/// Mirrors the <c>mkdocs-redirects</c> plugin shape. Mappings come
/// from three sources, merged at finalize time: the registration-time
/// <c>(from, to)</c> tuples, an optional <c>redirects.yml</c> file at
/// the input root (top-level <c>from: to</c> mapping), and per-page
/// <c>aliases:</c> frontmatter lists (each alias becomes a redirect
/// targeting that page).
/// </remarks>
public sealed class RedirectsPlugin : IDocPlugin
{
    /// <summary>Static <c>(from, to)</c> entries supplied at construction time.</summary>
    private readonly Dictionary<string, string> _seed;

    /// <summary>Aliases harvested from page frontmatter during the parallel render pass.</summary>
    private readonly ConcurrentDictionary<string, string> _aliases = new(StringComparer.Ordinal);

    /// <summary>Plugin options.</summary>
    private readonly RedirectsOptions _options;

    /// <summary>Captured input root for config-file lookup.</summary>
    private string _inputRoot = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class with default options and no static entries.</summary>
    public RedirectsPlugin()
        : this(RedirectsOptions.Default, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class with the given <paramref name="entries"/> and default options.</summary>
    /// <param name="entries">Tuples of <c>(fromPath, toUrl)</c>.</param>
    public RedirectsPlugin(params (string From, string To)[] entries)
        : this(RedirectsOptions.Default, entries)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class with explicit options and static entries.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="entries">Static tuples of <c>(fromPath, toUrl)</c> merged with config-file and alias entries at finalize.</param>
    public RedirectsPlugin(in RedirectsOptions options, (string From, string To)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _options = options;
        _seed = new(entries.Length, StringComparer.Ordinal);
        for (var i = 0; i < entries.Length; i++)
        {
            var (from, to) = entries[i];
            if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
            {
                _seed[from] = to;
            }
        }
    }

    /// <inheritdoc/>
    public string Name => "redirects";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _inputRoot = context.InputRoot;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_options.ScanFrontmatterAliases || string.IsNullOrEmpty(_options.AliasFrontmatterKey))
        {
            return ValueTask.CompletedTask;
        }

        var aliases = ExtractAliases(context.Source.Span, _options.AliasFrontmatterKey);
        if (aliases.Length is 0)
        {
            return ValueTask.CompletedTask;
        }

        var pageUrl = ToHtmlUrl(context.RelativePath);
        for (var i = 0; i < aliases.Length; i++)
        {
            var alias = NormalizeAlias(aliases[i]);
            if (alias.Length > 0)
            {
                _aliases[alias] = pageUrl;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        var merged = new Dictionary<string, string>(_seed.Count + _aliases.Count, StringComparer.Ordinal);
        foreach (var entry in _seed)
        {
            merged[entry.Key] = entry.Value;
        }

        if (_options.LoadConfigFile && !string.IsNullOrEmpty(_options.ConfigFileName) && !string.IsNullOrEmpty(_inputRoot))
        {
            var configPath = Path.Combine(_inputRoot, _options.ConfigFileName);
            if (File.Exists(configPath))
            {
                var bytes = await File.ReadAllBytesAsync(configPath, cancellationToken).ConfigureAwait(false);
                LoadFlatYaml(bytes, merged);
            }
        }

        foreach (var entry in _aliases)
        {
            // Static entries win over per-page aliases on conflict.
            if (!merged.ContainsKey(entry.Key))
            {
                merged[entry.Key] = entry.Value;
            }
        }

        if (merged.Count is 0)
        {
            return;
        }

        foreach (var entry in merged)
        {
            await WriteStubAsync(context.OutputRoot, entry.Key, entry.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reads a flat <c>key: value</c> top-level YAML mapping into <paramref name="sink"/>.</summary>
    /// <param name="bytes">UTF-8 YAML bytes.</param>
    /// <param name="sink">Destination dictionary; existing keys are overwritten when re-declared in the file.</param>
    private static void LoadFlatYaml(ReadOnlySpan<byte> bytes, Dictionary<string, string> sink)
    {
        var cursor = 0;
        while (cursor < bytes.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(bytes, cursor);
            var line = bytes[cursor..lineEnd];
            cursor = lineEnd;
            if (!YamlByteScanner.IsTopLevelKey(line))
            {
                continue;
            }

            var key = YamlByteScanner.KeyOf(line);
            var colon = line.IndexOf((byte)':');
            var rawValue = YamlByteScanner.TrimWhitespace(line[(colon + 1)..]);
            var value = YamlByteScanner.Unquote(rawValue);
            if (key.IsEmpty || value.IsEmpty)
            {
                continue;
            }

            sink[Encoding.UTF8.GetString(key)] = Encoding.UTF8.GetString(value);
        }
    }

    /// <summary>Pulls the inline-list or block-list values of <paramref name="key"/> from the frontmatter of <paramref name="source"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes (frontmatter + body).</param>
    /// <param name="key">Top-level frontmatter key.</param>
    /// <returns>Each list entry as a UTF-16 string; empty when the key is absent or has no list value.</returns>
    private static string[] ExtractAliases(ReadOnlySpan<byte> source, string key)
    {
        if (!source.StartsWith(YamlByteScanner.FrontmatterDelimiter))
        {
            return [];
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var cursor = YamlByteScanner.LineEnd(source, 0);
        while (cursor < source.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(source, cursor);
            var line = source[cursor..lineEnd];
            if (line.TrimEnd((byte)'\n').TrimEnd((byte)'\r').SequenceEqual(YamlByteScanner.FrontmatterDelimiter))
            {
                return [];
            }

            if (!YamlByteScanner.IsTopLevelKey(line)
                || !YamlByteScanner.KeyOf(line).SequenceEqual(keyBytes))
            {
                cursor = lineEnd;
                continue;
            }

            var colon = line.IndexOf((byte)':');
            var inline = YamlByteScanner.TrimWhitespace(line[(colon + 1)..]);
            if (inline is [(byte)'[', .., (byte)']'])
            {
                return ParseInlineList(inline[1..^1]);
            }

            return ParseBlockList(source, lineEnd);
        }

        return [];
    }

    /// <summary>Parses an inline YAML list (<c>[a, b, c]</c>) into an array of strings.</summary>
    /// <param name="span">Inner bytes (without the surrounding brackets).</param>
    /// <returns>One string per comma-separated entry.</returns>
    private static string[] ParseInlineList(ReadOnlySpan<byte> span)
    {
        var result = new List<string>(4);
        var start = 0;
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == (byte)',')
            {
                var slice = YamlByteScanner.TrimWhitespace(span[start..i]);
                slice = YamlByteScanner.Unquote(slice);
                if (!slice.IsEmpty)
                {
                    result.Add(Encoding.UTF8.GetString(slice));
                }

                start = i + 1;
            }
        }

        return [.. result];
    }

    /// <summary>Parses indented <c>- value</c> rows starting at <paramref name="cursor"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Offset of the first line below the key line.</param>
    /// <returns>One string per list entry.</returns>
    private static string[] ParseBlockList(ReadOnlySpan<byte> source, int cursor)
    {
        var result = new List<string>(4);
        while (cursor < source.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(source, cursor);
            var line = source[cursor..lineEnd];
            if (line.IsEmpty)
            {
                break;
            }

            var first = line[0];
            if (first is not ((byte)' ' or (byte)'\t' or (byte)'-'))
            {
                break;
            }

            var trimmed = YamlByteScanner.TrimLeading(line);
            if (trimmed is not [(byte)'-', ..])
            {
                cursor = lineEnd;
                continue;
            }

            var entry = YamlByteScanner.TrimWhitespace(trimmed[1..]);
            entry = YamlByteScanner.Unquote(entry);
            if (!entry.IsEmpty)
            {
                result.Add(Encoding.UTF8.GetString(entry));
            }

            cursor = lineEnd;
        }

        return [.. result];
    }

    /// <summary>Translates a source-relative markdown path to its rendered HTML URL.</summary>
    /// <param name="markdownPath">Source-relative path (e.g. <c>guide/intro.md</c>).</param>
    /// <returns>Site-relative URL with the <c>.html</c> extension and forward slashes.</returns>
    private static string ToHtmlUrl(string markdownPath)
    {
        if (markdownPath is [])
        {
            return string.Empty;
        }

        var withoutExt = Path.ChangeExtension(markdownPath, ".html");
        return withoutExt.Replace('\\', '/');
    }

    /// <summary>Normalizes an alias entry to a forward-slashed path with an <c>.html</c> extension.</summary>
    /// <param name="alias">Raw alias value.</param>
    /// <returns>Normalized alias path.</returns>
    private static string NormalizeAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return string.Empty;
        }

        var trimmed = alias.Trim().Replace('\\', '/');
        if (trimmed.EndsWith('/'))
        {
            return trimmed + "index.html";
        }

        return trimmed.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + ".html";
    }

    /// <summary>Writes a single redirect stub to <paramref name="outputRoot"/>/<paramref name="fromPath"/>.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="fromPath">Relative path of the redirect stub.</param>
    /// <param name="toUrl">Destination URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the stub has been written.</returns>
    private static async Task WriteStubAsync(string outputRoot, string fromPath, string toUrl, CancellationToken cancellationToken)
    {
        var absolute = Path.GetFullPath(Path.Combine(outputRoot, fromPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        var html = BuildStub(toUrl);
        await File.WriteAllBytesAsync(absolute, Encoding.UTF8.GetBytes(html), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the meta-refresh HTML targeting <paramref name="toUrl"/>.</summary>
    /// <param name="toUrl">Destination URL.</param>
    /// <returns>The HTML stub.</returns>
    private static string BuildStub(string toUrl)
    {
        var escaped = HtmlAttributeEscape(toUrl);
        return $"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Redirecting…</title>
<meta http-equiv="refresh" content="0; url={escaped}">
<link rel="canonical" href="{escaped}">
<meta name="robots" content="noindex">
</head>
<body>
<p>Redirecting to <a href="{escaped}">{escaped}</a>…</p>
</body>
</html>
""";
    }

    /// <summary>Escapes <paramref name="value"/> for use inside an HTML attribute.</summary>
    /// <param name="value">Source string.</param>
    /// <returns>Escaped string.</returns>
    private static string HtmlAttributeEscape(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            sb.Append(value[i] switch
            {
                '"' => "&quot;",
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => value[i].ToString(),
            });
        }

        return sb.ToString();
    }
}
