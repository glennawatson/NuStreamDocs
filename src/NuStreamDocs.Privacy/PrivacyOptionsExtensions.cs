// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy;

/// <summary>String / span construction helpers for the byte-shaped <see cref="PrivacyOptions"/> record.</summary>
public static class PrivacyOptionsExtensions
{
    /// <summary>Extension members for <c>PrivacyOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in PrivacyOptions options)
    {
        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="assetDirectory"/> as the new asset directory.</summary>
        /// <param name="assetDirectory">New directory.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithAssetDirectory(in DirectoryPath assetDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(assetDirectory.Value);
            return options with { AssetDirectory = Encoding.UTF8.GetBytes(assetDirectory) };
        }

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="assetDirectory"/> as the new asset directory.</summary>
        /// <param name="assetDirectory">New directory bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions
            WithAssetDirectory(ReadOnlySpan<byte> assetDirectory) =>
            options with { AssetDirectory = assetDirectory.ToArray() };

        /// <summary>Replaces the skip list with <paramref name="hosts"/>; clears the well-known defaults too.</summary>
        /// <param name="hosts">Host strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithHostsToSkip(params string[] hosts) =>
            options with { HostsToSkip = hosts.EncodeUtf8Array() };

        /// <summary>Replaces the skip list with the supplied UTF-8 host bytes.</summary>
        /// <param name="hosts">Host bytes (one entry per host).</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithHostsToSkip(params byte[][] hosts) =>
            options with { HostsToSkip = hosts };

        /// <summary>Appends <paramref name="hosts"/> to the existing skip list — keeps the well-known defaults.</summary>
        /// <param name="hosts">Additional host strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddHostsToSkip(params string[] hosts) =>
            hosts.Length is 0
                ? options
                : options.AppendHostsToSkip(hosts.EncodeUtf8Array());

        /// <summary>Appends UTF-8 <paramref name="hosts"/> to the existing skip list.</summary>
        /// <param name="hosts">Additional host bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddHostsToSkip(params byte[][] hosts) =>
            hosts.Length is 0
                ? options
                : options.AppendHostsToSkip(hosts);

        /// <summary>Appends a single UTF-8 host (e.g. a <c>"..."u8</c> literal) to the existing skip list.</summary>
        /// <param name="host">UTF-8 host bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PrivacyOptions AddHostsToSkip(ReadOnlySpan<byte> host) =>
            options.AppendHostsToSkip([host.ToArray()]);

        /// <summary>Empties the skip list — strips both the well-known defaults and any caller-added entries.</summary>
        /// <returns>The updated options.</returns>
        public PrivacyOptions ClearHostsToSkip() =>
            options with { HostsToSkip = [] };

        /// <summary>Replaces the allow list with <paramref name="hosts"/>.</summary>
        /// <param name="hosts">Host strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithHostsAllowed(params string[] hosts) =>
            options with { HostsAllowed = hosts.EncodeUtf8Array() };

        /// <summary>Replaces the allow list with the supplied UTF-8 host bytes.</summary>
        /// <param name="hosts">Host bytes (one entry per host).</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithHostsAllowed(params byte[][] hosts) =>
            options with { HostsAllowed = hosts };

        /// <summary>Appends <paramref name="hosts"/> to the existing allow list.</summary>
        /// <param name="hosts">Additional host strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddHostsAllowed(params string[] hosts) =>
            hosts.Length is 0
                ? options
                : options.AppendHostsAllowed(hosts.EncodeUtf8Array());

        /// <summary>Appends UTF-8 <paramref name="hosts"/> to the existing allow list.</summary>
        /// <param name="hosts">Additional host bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddHostsAllowed(params byte[][] hosts) =>
            hosts.Length is 0
                ? options
                : options.AppendHostsAllowed(hosts);

        /// <summary>Appends a single UTF-8 host (e.g. a <c>"..."u8</c> literal) to the existing allow list.</summary>
        /// <param name="host">UTF-8 host bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PrivacyOptions AddHostsAllowed(ReadOnlySpan<byte> host) =>
            options.AppendHostsAllowed([host.ToArray()]);

        /// <summary>Empties the allow list.</summary>
        /// <returns>The updated options.</returns>
        public PrivacyOptions ClearHostsAllowed() =>
            options with { HostsAllowed = [] };

        /// <summary>Replaces the URL include list with <paramref name="patterns"/>.</summary>
        /// <param name="patterns">Glob pattern strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithUrlIncludePatterns(params string[] patterns) =>
            options with { UrlIncludePatterns = patterns.EncodeUtf8Array() };

        /// <summary>Replaces the URL include list with the supplied UTF-8 pattern bytes.</summary>
        /// <param name="patterns">Glob pattern bytes (one entry per pattern).</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithUrlIncludePatterns(params byte[][] patterns) =>
            options with { UrlIncludePatterns = patterns };

        /// <summary>Appends <paramref name="patterns"/> to the existing URL include list.</summary>
        /// <param name="patterns">Additional glob pattern strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddUrlIncludePatterns(params string[] patterns) =>
            patterns.Length is 0
                ? options
                : options.AppendUrlIncludePatterns(patterns.EncodeUtf8Array());

        /// <summary>Appends UTF-8 <paramref name="patterns"/> to the existing URL include list.</summary>
        /// <param name="patterns">Additional glob pattern bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddUrlIncludePatterns(params byte[][] patterns) =>
            patterns.Length is 0
                ? options
                : options.AppendUrlIncludePatterns(patterns);

        /// <summary>Appends a single UTF-8 URL pattern (e.g. a <c>"..."u8</c> literal) to the existing include list.</summary>
        /// <param name="pattern">UTF-8 glob-pattern bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PrivacyOptions AddUrlIncludePatterns(ReadOnlySpan<byte> pattern) =>
            options.AppendUrlIncludePatterns([pattern.ToArray()]);

        /// <summary>Empties the URL include list.</summary>
        /// <returns>The updated options.</returns>
        public PrivacyOptions ClearUrlIncludePatterns() =>
            options with { UrlIncludePatterns = [] };

        /// <summary>Replaces the URL exclude list with <paramref name="patterns"/>.</summary>
        /// <param name="patterns">Glob pattern strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithUrlExcludePatterns(params string[] patterns) =>
            options with { UrlExcludePatterns = patterns.EncodeUtf8Array() };

        /// <summary>Replaces the URL exclude list with the supplied UTF-8 pattern bytes.</summary>
        /// <param name="patterns">Glob pattern bytes (one entry per pattern).</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithUrlExcludePatterns(params byte[][] patterns) =>
            options with { UrlExcludePatterns = patterns };

        /// <summary>Appends <paramref name="patterns"/> to the existing URL exclude list.</summary>
        /// <param name="patterns">Additional glob pattern strings.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddUrlExcludePatterns(params string[] patterns) =>
            patterns.Length is 0
                ? options
                : options.AppendUrlExcludePatterns(patterns.EncodeUtf8Array());

        /// <summary>Appends UTF-8 <paramref name="patterns"/> to the existing URL exclude list.</summary>
        /// <param name="patterns">Additional glob pattern bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions AddUrlExcludePatterns(params byte[][] patterns) =>
            patterns.Length is 0
                ? options
                : options.AppendUrlExcludePatterns(patterns);

        /// <summary>Appends a single UTF-8 URL pattern (e.g. a <c>"..."u8</c> literal) to the existing exclude list.</summary>
        /// <param name="pattern">UTF-8 glob-pattern bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PrivacyOptions AddUrlExcludePatterns(ReadOnlySpan<byte> pattern) =>
            options.AppendUrlExcludePatterns([pattern.ToArray()]);

        /// <summary>Empties the URL exclude list.</summary>
        /// <returns>The updated options.</returns>
        public PrivacyOptions ClearUrlExcludePatterns() =>
            options with { UrlExcludePatterns = [] };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the audit manifest path.</summary>
        /// <param name="path">Forward-slash relative path.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithAuditManifestPath(in FilePath path) =>
            options with { AuditManifestPath = Utf8Encoder.Encode(path) };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the audit manifest path.</summary>
        /// <param name="path">UTF-8 forward-slash relative path bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithAuditManifestPath(byte[] path) =>
            options with { AuditManifestPath = path };

        /// <summary>Replaces the audit manifest path with the supplied UTF-8 span.</summary>
        /// <param name="path">UTF-8 forward-slash relative path bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithAuditManifestPath(ReadOnlySpan<byte> path) =>
            options with { AuditManifestPath = path.ToArray() };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the cache directory.</summary>
        /// <param name="path">Absolute path; empty falls back to <c>{outputRoot}/.cache/privacy</c>.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithCacheDirectory(in DirectoryPath path) =>
            options with { CacheDirectory = Utf8Encoder.Encode(path) };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the cache directory.</summary>
        /// <param name="path">UTF-8 absolute path bytes; empty falls back to <c>{outputRoot}/.cache/privacy</c>.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithCacheDirectory(byte[] path) =>
            options with { CacheDirectory = path };

        /// <summary>Replaces the cache directory with the supplied UTF-8 span.</summary>
        /// <param name="path">UTF-8 absolute path bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithCacheDirectory(ReadOnlySpan<byte> path) =>
            options with { CacheDirectory = path.ToArray() };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the CSP manifest path.</summary>
        /// <param name="path">Forward-slash relative path.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithCspManifestPath(in FilePath path) =>
            options with { CspManifestPath = Utf8Encoder.Encode(path) };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the CSP manifest path.</summary>
        /// <param name="path">UTF-8 forward-slash relative path bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithCspManifestPath(byte[] path) =>
            options with { CspManifestPath = path };

        /// <summary>Replaces the CSP manifest path with the supplied UTF-8 span.</summary>
        /// <param name="path">UTF-8 forward-slash relative path bytes.</param>
        /// <returns>The updated options.</returns>
        public PrivacyOptions WithCspManifestPath(ReadOnlySpan<byte> path) =>
            options with { CspManifestPath = path.ToArray() };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="extra"/> appended to <see cref="PrivacyOptions.HostsToSkip"/>.</summary>
        /// <param name="extra">UTF-8 host bytes to append.</param>
        /// <returns>The updated options.</returns>
        private PrivacyOptions AppendHostsToSkip(byte[][] extra) =>
            options with { HostsToSkip = ArrayJoiner.Concat(options.HostsToSkip, extra) };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="extra"/> appended to <see cref="PrivacyOptions.HostsAllowed"/>.</summary>
        /// <param name="extra">UTF-8 host bytes to append.</param>
        /// <returns>The updated options.</returns>
        private PrivacyOptions AppendHostsAllowed(byte[][] extra) =>
            options with { HostsAllowed = ArrayJoiner.Concat(options.HostsAllowed, extra) };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="extra"/> appended to <see cref="PrivacyOptions.UrlIncludePatterns"/>.</summary>
        /// <param name="extra">UTF-8 pattern bytes to append.</param>
        /// <returns>The updated options.</returns>
        private PrivacyOptions AppendUrlIncludePatterns(byte[][] extra) =>
            options with { UrlIncludePatterns = ArrayJoiner.Concat(options.UrlIncludePatterns, extra) };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="extra"/> appended to <see cref="PrivacyOptions.UrlExcludePatterns"/>.</summary>
        /// <param name="extra">UTF-8 pattern bytes to append.</param>
        /// <returns>The updated options.</returns>
        private PrivacyOptions AppendUrlExcludePatterns(byte[][] extra) =>
            options with { UrlExcludePatterns = ArrayJoiner.Concat(options.UrlExcludePatterns, extra) };
    }
}
