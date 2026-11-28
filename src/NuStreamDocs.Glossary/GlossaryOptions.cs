// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Glossary;

/// <summary>Configuration options for <see cref="GlossaryPlugin"/>.</summary>
[System.Diagnostics.DebuggerDisplay("GlossaryOptions: {GlossaryFile}")]
public sealed record GlossaryOptions
{
    /// <summary>Gets the shared default glossary options.</summary>
    public static GlossaryOptions Default { get; } = new();

    /// <summary>Gets the optional glossary file path (YAML or JSON) resolved relative to the docs input root.</summary>
    public FilePath GlossaryFile { get; init; } = new("glossary.yml");

    /// <summary>Gets the output subdirectory under which the synthetic glossary index page is emitted.</summary>
    public PathSegment OutputSubdirectory { get; init; } = new("glossary");

    /// <summary>Gets the UTF-8 heading and frontmatter title of the generated glossary page.</summary>
    public byte[] PageTitle { get; init; } = [.. "Glossary"u8];

    /// <summary>Gets a value indicating whether to synthesize the central glossary Markdown page.</summary>
    public bool GenerateGlossaryPage { get; init; } = true;

    /// <summary>Gets a value indicating whether to wrap matching prose occurrences in accessible <c>&lt;abbr&gt;</c> tags.</summary>
    public bool AutoLinkTerms { get; init; } = true;

    /// <summary>Gets a value indicating whether each term is wrapped at most once per rendered HTML page.</summary>
    public bool FirstOccurrenceOnly { get; init; } = true;

    /// <summary>Gets programmatic glossary entries merged with any file-loaded entries.</summary>
    public GlossaryEntry[] Terms { get; init; } = [];
}
