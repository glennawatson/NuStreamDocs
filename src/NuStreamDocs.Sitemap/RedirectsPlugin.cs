// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;
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
    /// <summary>ASCII-only case offset between uppercase and lowercase letters.</summary>
    private const byte AsciiCaseOffset = 32;

    /// <summary>HTML extension bytes used to swap a markdown extension on the rendered page URL.</summary>
    private static readonly byte[] HtmlExtension = ".html"u8.ToArray();

    /// <summary>Markdown extension bytes recognized when computing the rendered URL.</summary>
    private static readonly byte[] MarkdownExtension = ".md"u8.ToArray();

    /// <summary>Trailing <c>index.html</c> appendix for directory-style aliases (<c>foo/</c>).</summary>
    private static readonly byte[] IndexHtml = "index.html"u8.ToArray();

    /// <summary>Static <c>(from, to)</c> entries supplied at construction time, encoded once to UTF-8.</summary>
    private readonly Dictionary<byte[], byte[]> _seed;

    /// <summary>Aliases harvested from page frontmatter during the parallel render pass.</summary>
    private readonly ConcurrentDictionary<byte[], byte[]> _aliases = new(ByteArrayComparer.Instance);

    /// <summary>Plugin options.</summary>
    private readonly RedirectsOptions _options;

    /// <summary>UTF-8 byte form of <see cref="RedirectsOptions.AliasFrontmatterKey"/>; encoded once at construction so the per-page alias scan never re-encodes.</summary>
    private readonly byte[] _aliasKeyBytes;

    /// <summary>Captured input root for config-file lookup.</summary>
    private DirectoryPath _inputRoot;

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class with default options and no static entries.</summary>
    public RedirectsPlugin()
        : this(RedirectsOptions.Default, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class with the given <paramref name="entries"/> and default options.</summary>
    /// <param name="entries">Tuples of <c>(fromPath, toUrl)</c>.</param>
    public RedirectsPlugin(params (UrlPath From, UrlPath To)[] entries)
        : this(RedirectsOptions.Default, entries)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RedirectsPlugin"/> class with explicit options and static entries.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="entries">Static tuples of <c>(fromPath, toUrl)</c> merged with config-file and alias entries at finalize.</param>
    public RedirectsPlugin(in RedirectsOptions options, (UrlPath From, UrlPath To)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _options = options;
        _aliasKeyBytes = Utf8Encoder.Encode(options.AliasFrontmatterKey);
        _seed = new(entries.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < entries.Length; i++)
        {
            var (from, to) = entries[i];
            if (!string.IsNullOrWhiteSpace(from.Value) && !string.IsNullOrWhiteSpace(to.Value))
            {
                _seed[Utf8Encoder.Encode(from)] = Utf8Encoder.Encode(to);
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
        if (!_options.ScanFrontmatterAliases || _aliasKeyBytes.Length is 0)
        {
            return ValueTask.CompletedTask;
        }

        var aliases = ExtractAliases(context.Source.Span, _aliasKeyBytes);
        if (aliases.Length is 0)
        {
            return ValueTask.CompletedTask;
        }

        var pageUrl = ToHtmlUrlBytes(context.RelativePath);
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
        var merged = new Dictionary<byte[], byte[]>(_seed.Count + _aliases.Count, ByteArrayComparer.Instance);
        foreach (var entry in _seed)
        {
            merged[entry.Key] = entry.Value;
        }

        if (_options.LoadConfigFile && !string.IsNullOrEmpty(_options.ConfigFileName) && !_inputRoot.IsEmpty)
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
    private static void LoadFlatYaml(ReadOnlySpan<byte> bytes, Dictionary<byte[], byte[]> sink)
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

            sink[key.ToArray()] = value.ToArray();
        }
    }

    /// <summary>Pulls the inline-list or block-list values of <paramref name="keyBytes"/> from the frontmatter of <paramref name="source"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes (frontmatter + body).</param>
    /// <param name="keyBytes">UTF-8 form of the top-level frontmatter key.</param>
    /// <returns>Each list entry as raw UTF-8 bytes; empty when the key is absent or has no list value.</returns>
    private static byte[][] ExtractAliases(ReadOnlySpan<byte> source, ReadOnlySpan<byte> keyBytes)
    {
        if (!source.StartsWith(YamlByteScanner.FrontmatterDelimiter))
        {
            return [];
        }

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

    /// <summary>Parses an inline YAML list (<c>[a, b, c]</c>) into raw UTF-8 byte arrays.</summary>
    /// <param name="span">Inner bytes (without the surrounding brackets).</param>
    /// <returns>One byte-array per comma-separated entry.</returns>
    private static byte[][] ParseInlineList(ReadOnlySpan<byte> span)
    {
        var result = new List<byte[]>(4);
        var start = 0;
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == (byte)',')
            {
                var slice = YamlByteScanner.TrimWhitespace(span[start..i]);
                slice = YamlByteScanner.Unquote(slice);
                if (!slice.IsEmpty)
                {
                    result.Add(slice.ToArray());
                }

                start = i + 1;
            }
        }

        return [.. result];
    }

    /// <summary>Parses indented <c>- value</c> rows starting at <paramref name="cursor"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Offset of the first line below the key line.</param>
    /// <returns>One byte-array per list entry.</returns>
    private static byte[][] ParseBlockList(ReadOnlySpan<byte> source, int cursor)
    {
        var result = new List<byte[]>(4);
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
                result.Add(entry.ToArray());
            }

            cursor = lineEnd;
        }

        return [.. result];
    }

    /// <summary>Translates a source-relative markdown path to its rendered HTML URL bytes.</summary>
    /// <param name="markdownPath">Source-relative path (e.g. <c>guide/intro.md</c>).</param>
    /// <returns>UTF-8 bytes of the site-relative URL with the <c>.html</c> extension and forward slashes; empty for an empty input.</returns>
    private static byte[] ToHtmlUrlBytes(string markdownPath)
    {
        if (markdownPath is [])
        {
            return [];
        }

        var byteCount = Encoding.UTF8.GetByteCount(markdownPath);
        var stripMd = markdownPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var output = new byte[stripMd ? byteCount - MarkdownExtension.Length + HtmlExtension.Length : byteCount];
        var written = stripMd
            ? Encoding.UTF8.GetBytes(markdownPath.AsSpan(0, markdownPath.Length - MarkdownExtension.Length), output)
            : Encoding.UTF8.GetBytes(markdownPath, output);
        if (stripMd)
        {
            HtmlExtension.CopyTo(output.AsSpan(written));
        }

        for (var i = 0; i < output.Length; i++)
        {
            if (output[i] is (byte)'\\')
            {
                output[i] = (byte)'/';
            }
        }

        return output;
    }

    /// <summary>Normalizes an alias entry to a forward-slashed UTF-8 path with an <c>.html</c> extension.</summary>
    /// <param name="alias">Raw alias bytes.</param>
    /// <returns>Normalized alias bytes; empty when input is whitespace-only.</returns>
    private static byte[] NormalizeAlias(ReadOnlySpan<byte> alias)
    {
        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(alias);
        if (trimmed.IsEmpty)
        {
            return [];
        }

        var suffix = ChooseSuffix(trimmed);
        var output = new byte[trimmed.Length + suffix.Length];
        for (var i = 0; i < trimmed.Length; i++)
        {
            var b = trimmed[i];
            output[i] = b is (byte)'\\' ? (byte)'/' : b;
        }

        suffix.CopyTo(output.AsSpan(trimmed.Length));
        return output;
    }

    /// <summary>Picks the suffix to append to a normalized alias body — <c>index.html</c> for directory-style, <c>.html</c> when an extension is missing, empty otherwise.</summary>
    /// <param name="trimmed">Whitespace-trimmed alias bytes; must be non-empty.</param>
    /// <returns>The suffix to append.</returns>
    private static ReadOnlySpan<byte> ChooseSuffix(ReadOnlySpan<byte> trimmed)
    {
        if (trimmed[^1] is (byte)'/' or (byte)'\\')
        {
            return IndexHtml;
        }

        return EndsWithHtml(trimmed) ? default : HtmlExtension;
    }

    /// <summary>Returns true when <paramref name="value"/> ends with <c>.html</c> (case-insensitive ASCII).</summary>
    /// <param name="value">Source bytes.</param>
    /// <returns>True for <c>.html</c> / <c>.HTML</c> / mixed-case variants.</returns>
    private static bool EndsWithHtml(ReadOnlySpan<byte> value)
    {
        if (value.Length < HtmlExtension.Length)
        {
            return false;
        }

        var tail = value[^HtmlExtension.Length..];
        for (var i = 0; i < HtmlExtension.Length; i++)
        {
            var a = tail[i];
            var b = HtmlExtension[i];
            if (a is >= (byte)'A' and <= (byte)'Z')
            {
                a += AsciiCaseOffset;
            }

            if (a != b)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Writes a single redirect stub to <paramref name="outputRoot"/>/<paramref name="fromPathBytes"/>.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="fromPathBytes">Relative path of the redirect stub as UTF-8 bytes.</param>
    /// <param name="toUrlBytes">Destination URL as UTF-8 bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the stub has been written.</returns>
    private static async Task WriteStubAsync(string outputRoot, byte[] fromPathBytes, byte[] toUrlBytes, CancellationToken cancellationToken)
    {
        var fromPath = Encoding.UTF8.GetString(fromPathBytes);
        var absolute = Path.GetFullPath(Path.Combine(outputRoot, fromPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        var sink = new ArrayBufferWriter<byte>(512);
        BuildStub(toUrlBytes, sink);
        await File.WriteAllBytesAsync(absolute, sink.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Composes the meta-refresh HTML targeting <paramref name="urlBytes"/> directly into <paramref name="sink"/>.</summary>
    /// <param name="urlBytes">Destination URL as UTF-8 bytes.</param>
    /// <param name="sink">UTF-8 sink the stub bytes are written into.</param>
    private static void BuildStub(ReadOnlySpan<byte> urlBytes, ArrayBufferWriter<byte> sink)
    {
        sink.Write("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Redirecting…</title>\n<meta http-equiv=\"refresh\" content=\"0; url="u8);
        XmlEntityEscaper.WriteEscaped(sink, urlBytes, XmlEntityEscaper.Mode.HtmlAttribute);
        sink.Write("\">\n<link rel=\"canonical\" href=\""u8);
        XmlEntityEscaper.WriteEscaped(sink, urlBytes, XmlEntityEscaper.Mode.HtmlAttribute);
        sink.Write("\">\n<meta name=\"robots\" content=\"noindex\">\n</head>\n<body>\n<p>Redirecting to <a href=\""u8);
        XmlEntityEscaper.WriteEscaped(sink, urlBytes, XmlEntityEscaper.Mode.HtmlAttribute);
        sink.Write("\">"u8);
        XmlEntityEscaper.WriteEscaped(sink, urlBytes, XmlEntityEscaper.Mode.HtmlAttribute);
        sink.Write("</a>…</p>\n</body>\n</html>\n"u8);
    }
}
