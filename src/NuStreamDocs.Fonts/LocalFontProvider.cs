// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Resolves a face from font files already present in the build's input directory, matched by glob.</summary>
public sealed class LocalFontProvider : IFontProvider
{
    /// <summary>Shared instance.</summary>
    public static readonly LocalFontProvider Instance = new();

    /// <summary>Initializes a new instance of the <see cref="LocalFontProvider"/> class.</summary>
    private LocalFontProvider()
    {
    }

    /// <inheritdoc/>
    public async ValueTask<FontResource[]> ResolveAsync(
        FontFace face,
        byte[][] requestedSubsets,
        FontDownloadCache cache,
        DirectoryPath inputRoot,
        bool[]? subsetUsage,
        CancellationToken cancellationToken)
    {
        _ = requestedSubsets;
        _ = cache;
        _ = subsetUsage;
        ArgumentException.ThrowIfNullOrEmpty(inputRoot.Value);

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        for (var i = 0; i < face.LocalSrc.Length; i++)
        {
            matcher.AddInclude(face.LocalSrc[i].Value);
        }

        var matches = matcher.Execute(new DirectoryInfoWrapper(new(inputRoot.Value)));
        var defaultWeight = face.Weights is [var first, ..] ? first : 400;
        var defaultStyle = face.Styles is [var firstStyle, ..] ? firstStyle : FontStyle.Normal;
        List<FontResource> resources = [];
        foreach (var file in matches.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(inputRoot.Value, file.Path);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var style = file.Path.Contains("italic", StringComparison.OrdinalIgnoreCase)
                ? FontStyle.Italic
                : defaultStyle;
            resources.Add(
                new(
                    (byte[])face.FamilyBytes.Clone(),
                    defaultWeight,
                    style,
                    [],
                    bytes,
                    (ApiCompatString)fullPath));
        }

        if (resources is [])
        {
            throw new FontDownloadException(
                StringCompose.Concat(
                    "No local font files matched for family ",
                    Encoding.UTF8.GetString(face.FamilyBytes),
                    " under ",
                    inputRoot.Value));
        }

        return [.. resources];
    }
}
