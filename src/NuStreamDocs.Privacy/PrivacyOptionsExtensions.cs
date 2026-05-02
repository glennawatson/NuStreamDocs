// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy;

/// <summary>String / span construction helpers for the byte-shaped <see cref="PrivacyOptions"/> record.</summary>
/// <remarks>
/// Encodes the inputs once at construction so the per-page hot path stays byte-only. Callers building
/// from YAML/TOML config readers (which produce strings) reach for the <c>WithXxx</c> overloads;
/// callers with byte-literal sources construct the record directly with <c>"..."u8.ToArray()</c>.
/// </remarks>
public static class PrivacyOptionsExtensions
{
    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="assetDirectory"/> as the new asset directory.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="assetDirectory">New directory.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithAssetDirectory(this PrivacyOptions options, string assetDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetDirectory);
        return options with { AssetDirectory = Encoding.UTF8.GetBytes(assetDirectory) };
    }

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="assetDirectory"/> as the new asset directory.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="assetDirectory">New directory bytes.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithAssetDirectory(this PrivacyOptions options, ReadOnlySpan<byte> assetDirectory) =>
        options with { AssetDirectory = assetDirectory.ToArray() };

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="hosts"/> as the skip list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="hosts">Host strings.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithHostsToSkip(this PrivacyOptions options, params string[] hosts) =>
        options with { HostsToSkip = ToUtf8Array(hosts) };

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="hosts"/> as the allow list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="hosts">Host strings.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithHostsAllowed(this PrivacyOptions options, params string[] hosts) =>
        options with { HostsAllowed = ToUtf8Array(hosts) };

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="patterns"/> as the URL include list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Glob pattern strings.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithUrlIncludePatterns(this PrivacyOptions options, params string[] patterns) =>
        options with { UrlIncludePatterns = ToUtf8Array(patterns) };

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="patterns"/> as the URL exclude list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Glob pattern strings.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithUrlExcludePatterns(this PrivacyOptions options, params string[] patterns) =>
        options with { UrlExcludePatterns = ToUtf8Array(patterns) };

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the audit manifest path.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="path">Forward-slash relative path.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithAuditManifestPath(this PrivacyOptions options, string path) =>
        options with { AuditManifestPath = string.IsNullOrEmpty(path) ? [] : Encoding.UTF8.GetBytes(path) };

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the cache directory.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="path">Absolute path; empty falls back to <c>{outputRoot}/.cache/privacy</c>.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithCacheDirectory(this PrivacyOptions options, string path) =>
        options with { CacheDirectory = string.IsNullOrEmpty(path) ? [] : Encoding.UTF8.GetBytes(path) };

    /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="path"/> as the CSP manifest path.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="path">Forward-slash relative path.</param>
    /// <returns>The updated options.</returns>
    public static PrivacyOptions WithCspManifestPath(this PrivacyOptions options, string path) =>
        options with { CspManifestPath = string.IsNullOrEmpty(path) ? [] : Encoding.UTF8.GetBytes(path) };

    /// <summary>Encodes every entry of <paramref name="values"/> into a fresh UTF-8 byte-array snapshot.</summary>
    /// <param name="values">Source strings; null/empty maps to an empty array.</param>
    /// <returns>Byte-array snapshot.</returns>
    private static byte[][] ToUtf8Array(string[]? values)
    {
        if (values is null or [])
        {
            return [];
        }

        var result = new byte[values.Length][];
        for (var i = 0; i < values.Length; i++)
        {
            ArgumentException.ThrowIfNullOrEmpty(values[i]);
            result[i] = Encoding.UTF8.GetBytes(values[i]);
        }

        return result;
    }
}
