// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Xrefs;

/// <summary>One external xrefmap to import at configure time.</summary>
/// <param name="Source">Local <c>xrefmap.json</c> path or <c>http(s)://</c> URL.</param>
/// <param name="BaseUrl">URL prefix prepended to every imported <c>href</c>; overrides the file's embedded <c>baseUrl</c>. Empty leaves hrefs untouched.</param>
public readonly record struct XrefImport(string Source, string BaseUrl)
{
    /// <summary>Initializes a new instance of the <see cref="XrefImport"/> struct without a base-URL override.</summary>
    /// <param name="source">Local path or URL.</param>
    public XrefImport(string source)
        : this(source, string.Empty)
    {
    }
}
