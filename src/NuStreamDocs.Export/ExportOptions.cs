// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Export;

/// <summary>Configuration options for <see cref="ExportPlugin"/>.</summary>
[System.Diagnostics.DebuggerDisplay("ExportOptions: {OutputFileName}")]
public sealed record ExportOptions
{
    /// <summary>Gets the shared default export options.</summary>
    public static ExportOptions Default { get; } = new();

    /// <summary>Gets the output filename written under the site output root.</summary>
    public string OutputFileName { get; init; } = "print.html";

    /// <summary>Gets the UTF-8 cover title of the consolidated export document.</summary>
    public byte[] DocumentTitle { get; init; } = [.. "Documentation"u8];

    /// <summary>Gets the optional UTF-8 cover subtitle.</summary>
    public byte[] Subtitle { get; init; } = [];

    /// <summary>Gets a value indicating whether to emit a consolidated table of contents before the chapters.</summary>
    public bool IncludeTableOfContents { get; init; } = true;
}
