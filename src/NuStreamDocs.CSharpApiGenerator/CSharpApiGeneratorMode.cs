// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Selects how <see cref="CSharpApiGeneratorPlugin"/> hands API metadata
/// to the rest of the build pipeline.
/// </summary>
public enum CSharpApiGeneratorMode
{
    /// <summary>
    /// Run SourceDocParser's emit pipeline; write Markdown to
    /// <c>{docsInputRoot}/{OutputMarkdownSubdirectory}</c> so the standard
    /// page-discovery pass picks the files up like author-written docs.
    /// </summary>
    EmitMarkdown,

    /// <summary>
    /// Run SourceDocParser's direct-extract pipeline (<c>ExtractAsync</c>);
    /// keep the canonical <c>ApiType[]</c> in memory for downstream plugins
    /// to consume without writing intermediate Markdown to disk.
    /// </summary>
    Direct
}
