// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Caching;

/// <summary>
/// Collection of <see cref="ManifestEntry"/> records persisted between
/// builds and consulted at the start of each build to skip unchanged
/// pages.
/// </summary>
/// <remarks>
/// On-disk shape is a small UTF-8 JSON file under the output root —
/// readable, diffable, schema-loose. Read via <see cref="JsonDocument"/>
/// (no reflection) so the AOT-clean rule holds; written via
/// <see cref="Utf8JsonWriter"/> straight to <c>FileStream</c>.
/// In-memory the entries live in a <see cref="Dictionary{TKey,TValue}"/>
/// keyed by relative path for O(1) lookup during the parallel render
/// stage.
/// </remarks>
public sealed class BuildManifest
{
    /// <summary>Schema version emitted in the JSON document; bumped on breaking changes.</summary>
    private const int SchemaVersion = 2;

    /// <summary>Raw SHA-256 fingerprint bytes of the pipeline that produced these entries.</summary>
    private readonly byte[] _buildFingerprint;

    /// <summary>Relative-path → entry lookup, frozen for read-mostly access.</summary>
    private Dictionary<FilePath, ManifestEntry> _entries;

    /// <summary>Initializes a new instance of the <see cref="BuildManifest"/> class.</summary>
    /// <param name="entries">Entries to seed the manifest with.</param>
    /// <param name="buildFingerprint">Fingerprint of the pipeline that produced <paramref name="entries"/>.</param>
    private BuildManifest(Dictionary<FilePath, ManifestEntry> entries, byte[] buildFingerprint)
    {
        _entries = entries;
        _buildFingerprint = buildFingerprint;
    }

    /// <summary>Gets the file name written under the output root.</summary>
    public static string FileName => ".nustreamdocs.manifest.json";

    /// <summary>Gets the number of entries currently tracked.</summary>
    public int Count => _entries.Count;

    /// <summary>Returns an empty manifest.</summary>
    /// <returns>A manifest with no entries.</returns>
    public static BuildManifest Empty() =>
        new(EmptyCollections.DictionaryFor<FilePath, ManifestEntry>(), []);

    /// <summary>Returns an empty manifest bound to <paramref name="buildFingerprint"/>.</summary>
    /// <param name="buildFingerprint">Current pipeline fingerprint bytes.</param>
    /// <returns>A manifest with no tracked entries.</returns>
    public static BuildManifest Empty(byte[] buildFingerprint)
    {
        ArgumentNullException.ThrowIfNull(buildFingerprint);
        return new(EmptyCollections.DictionaryFor<FilePath, ManifestEntry>(), buildFingerprint);
    }

    /// <summary>
    /// Loads the manifest from <paramref name="outputRoot"/>; returns an
    /// empty manifest when no file exists or it cannot be parsed.
    /// </summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded manifest.</returns>
    public static ValueTask<BuildManifest> LoadAsync(DirectoryPath outputRoot, in CancellationToken cancellationToken) =>
        LoadAsync(outputRoot, cancellationToken, logger: null);

    /// <summary>Loads the manifest and rejects it when its build fingerprint differs from <paramref name="buildFingerprint"/>.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="buildFingerprint">Current pipeline fingerprint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded manifest, or an empty manifest on a fingerprint mismatch.</returns>
    public static ValueTask<BuildManifest> LoadAsync(DirectoryPath outputRoot, byte[] buildFingerprint, in CancellationToken cancellationToken) =>
        LoadAsync(outputRoot, buildFingerprint, cancellationToken, logger: null);

    /// <summary>
    /// Loads the manifest from <paramref name="outputRoot"/> with an optional logger;
    /// returns an empty manifest when no file exists or it cannot be parsed.
    /// </summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="logger">Optional logger; pass <see langword="null"/> to silence diagnostics.</param>
    /// <returns>The loaded manifest.</returns>
    public static async ValueTask<BuildManifest> LoadAsync(DirectoryPath outputRoot, CancellationToken cancellationToken, ILogger? logger)
    {
        if (outputRoot.IsEmpty)
        {
            throw new ArgumentException("Output root must be non-empty.", nameof(outputRoot));
        }

        var log = logger ?? NullLogger.Instance;
        var path = Path.Combine(outputRoot, FileName);
        if (!File.Exists(path))
        {
            return Empty();
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var manifest = Parse(bytes);
            CachingLoggingHelper.LogManifestLoaded(log, path, manifest.Count);
            return manifest;
        }
        catch (JsonException)
        {
            // Corrupt manifest — fall back to a full rebuild rather than fail.
            return Empty();
        }
    }

    /// <summary>Loads the manifest with an expected pipeline fingerprint.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="buildFingerprint">Current pipeline fingerprint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="logger">Optional logger; pass <see langword="null"/> to silence diagnostics.</param>
    /// <returns>The loaded manifest when the fingerprint matches; otherwise an empty manifest.</returns>
    public static async ValueTask<BuildManifest> LoadAsync(
        DirectoryPath outputRoot,
        byte[] buildFingerprint,
        CancellationToken cancellationToken,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(buildFingerprint);
        var manifest = await LoadAsync(outputRoot, cancellationToken, logger).ConfigureAwait(false);
        return manifest._buildFingerprint.AsSpan().SequenceEqual(buildFingerprint)
            ? manifest
            : Empty(buildFingerprint);
    }

    /// <summary>Looks up an entry by relative path.</summary>
    /// <param name="relativePath">Relative path key.</param>
    /// <param name="entry">Found entry on success.</param>
    /// <returns>True when an entry was found.</returns>
    public bool TryGet(FilePath relativePath, out ManifestEntry entry) =>
        _entries.TryGetValue(relativePath, out entry);

    /// <summary>Replaces the entries with <paramref name="updated"/>.</summary>
    /// <param name="updated">Right-sized array of entries to store.</param>
    public void Replace(ManifestEntry[] updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        _entries = ManifestIndex.Build(updated);
    }

    /// <summary>Replaces the entries by draining <paramref name="updated"/> in a single pass.</summary>
    /// <param name="updated">Concurrent queue of fresh entries; ownership transfers to the manifest.</param>
    /// <remarks>
    /// <see cref="ConcurrentQueue{T}.ToArray"/> performs a single pre-sized
    /// allocation off the queue's segmented backing storage — cheaper than
    /// the <c>[.. bag]</c> spread that materializes through an enumerator.
    /// </remarks>
    public void Replace(ConcurrentQueue<ManifestEntry> updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        ManifestEntry[] snapshot = [.. updated];
        _entries = ManifestIndex.Build(snapshot);
    }

    /// <summary>Persists the manifest under <paramref name="outputRoot"/>.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public Task SaveAsync(DirectoryPath outputRoot, in CancellationToken cancellationToken) =>
        SaveAsync(outputRoot, cancellationToken, logger: null);

    /// <summary>Persists the manifest under <paramref name="outputRoot"/> with an optional logger.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="logger">Optional logger; pass <see langword="null"/> to silence diagnostics.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public async Task SaveAsync(DirectoryPath outputRoot, CancellationToken cancellationToken, ILogger? logger)
    {
        if (outputRoot.IsEmpty)
        {
            throw new ArgumentException("Output root must be non-empty.", nameof(outputRoot));
        }

        Directory.CreateDirectory(outputRoot);

        var log = logger ?? NullLogger.Instance;
        var path = Path.Combine(outputRoot, FileName);
        await using var stream = File.Create(path);
        await using Utf8JsonWriter writer = new(stream);
        writer.WriteStartObject();
        writer.WriteNumber("schema"u8, SchemaVersion);
        writer.WriteBase64String("build"u8, _buildFingerprint);

        writer.WritePropertyName("entries"u8);
        writer.WriteStartArray();
        foreach (var pair in _entries)
        {
            var entry = pair.Value;
            writer.WriteStartObject();
            writer.WriteString("path"u8, entry.RelativePath);
            writer.WriteBase64String("hash"u8, entry.ContentHash);
            writer.WriteNumber("len"u8, entry.OutputLengthBytes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        CachingLoggingHelper.LogManifestSaved(log, path, _entries.Count);
    }

    /// <summary>Parses the manifest from a UTF-8 JSON byte buffer.</summary>
    /// <param name="utf8">UTF-8 JSON bytes.</param>
    /// <returns>The parsed manifest, or an empty manifest on a schema mismatch.</returns>
    private static BuildManifest Parse(ReadOnlySpan<byte> utf8)
    {
        Utf8JsonReader reader = new(utf8, isFinalBlock: true, state: default);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!TryReadDocument(root, out var buildFingerprint, out var entries))
        {
            return Empty();
        }

        if (entries.GetArrayLength() is 0)
        {
            return new(EmptyCollections.DictionaryFor<FilePath, ManifestEntry>(), buildFingerprint);
        }

        var buffer = new ManifestEntry[entries.GetArrayLength()];
        var count = ReadEntries(entries, buffer);
        if (count == 0)
        {
            return new(EmptyCollections.DictionaryFor<FilePath, ManifestEntry>(), buildFingerprint);
        }

        if (count != buffer.Length)
        {
            Array.Resize(ref buffer, count);
        }

        return new(ManifestIndex.Build(buffer), buildFingerprint);
    }

    /// <summary>Validates the document shape and exposes the build fingerprint and entries array.</summary>
    /// <param name="root">Root JSON element.</param>
    /// <param name="buildFingerprint">Build fingerprint on success.</param>
    /// <param name="entries">Entries array on success.</param>
    /// <returns>True when the document matches the expected schema.</returns>
    private static bool TryReadDocument(in JsonElement root, out byte[] buildFingerprint, out JsonElement entries)
    {
        buildFingerprint = [];
        entries = default;
        if (!root.TryGetProperty("schema"u8, out var schema) ||
            schema.ValueKind != JsonValueKind.Number ||
            schema.GetInt32() != SchemaVersion)
        {
            return false;
        }

        if (!root.TryGetProperty("build"u8, out var buildProp) || buildProp.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        byte[] fingerprint;
        try
        {
            fingerprint = buildProp.GetBytesFromBase64();
        }
        catch (FormatException)
        {
            return false;
        }

        if (fingerprint is [])
        {
            return false;
        }

        if (!root.TryGetProperty("entries"u8, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        buildFingerprint = fingerprint;
        entries = array;
        return true;
    }

    /// <summary>Materializes every well-formed entry from the JSON array.</summary>
    /// <param name="entries">Entries JSON array.</param>
    /// <param name="buffer">Destination buffer sized to the array length.</param>
    /// <returns>Number of entries written.</returns>
    private static int ReadEntries(in JsonElement entries, ManifestEntry[] buffer)
    {
        var count = 0;
        var iter = entries.EnumerateArray();
        while (iter.MoveNext())
        {
            if (TryReadEntry(iter.Current, out var entry))
            {
                buffer[count++] = entry;
            }
        }

        return count;
    }

    /// <summary>Reads one entry object.</summary>
    /// <param name="item">JSON object element.</param>
    /// <param name="entry">Parsed entry on success.</param>
    /// <returns>True when the object had every required property.</returns>
    private static bool TryReadEntry(in JsonElement item, out ManifestEntry entry)
    {
        entry = default;
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("path"u8, out var pathProp) ||
            !item.TryGetProperty("hash"u8, out var hashProp) ||
            !item.TryGetProperty("len"u8, out var lenProp))
        {
            return false;
        }

        var path = pathProp.GetString();
        if (path is null || hashProp.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        byte[] hash;
        try
        {
            hash = hashProp.GetBytesFromBase64();
        }
        catch (FormatException)
        {
            return false;
        }

        entry = new(path, hash, lenProp.GetInt64());
        return true;
    }
}
