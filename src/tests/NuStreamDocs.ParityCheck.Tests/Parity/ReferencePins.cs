// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The exact package versions the reference engines run with.</summary>
internal static class ReferencePins
{
    /// <summary>Pinned MkDocs release.</summary>
    internal const string MkDocs = "1.6.1";

    /// <summary>Pinned Zensical release.</summary>
    internal const string Zensical = "0.0.63";

    /// <summary>Pinned Python-Markdown release both engines render through.</summary>
    internal const string Markdown = "3.10.3";

    /// <summary>Pinned pymdown-extensions release Zensical renders through.</summary>
    internal const string PyMdownExtensions = "12.0.1";

    /// <summary>Gets the pinned packages as normalized name and version pairs.</summary>
    internal static PackagePin[] Packages { get; } =
    [
        new("mkdocs", MkDocs),
        new("zensical", Zensical),
        new("markdown", Markdown),
        new("pymdown-extensions", PyMdownExtensions),
    ];

    /// <summary>Gets the <c>name==version</c> requirement specifiers passed to the package installer.</summary>
    /// <returns>One specifier per pinned package.</returns>
    internal static string[] Requirements()
    {
        var requirements = new string[Packages.Length];
        for (var i = 0; i < Packages.Length; i++)
        {
            requirements[i] = $"{Packages[i].Name}=={Packages[i].Version}";
        }

        return requirements;
    }
}
