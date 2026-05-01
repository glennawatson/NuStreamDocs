// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Xrefs;

/// <summary>
/// One external xrefmap to import at configure time.
/// </summary>
/// <param name="Source">
/// Either an absolute path to a local <c>xrefmap.json</c> on disk, or
/// an <c>http://</c> / <c>https://</c> URL the plugin will fetch.
/// </param>
/// <param name="BaseUrl">
/// URL prefix prepended to every imported entry's <c>href</c>. When
/// the imported file's <c>baseUrl</c> field is set we use that; this
/// option overrides it. Empty leaves the imported hrefs as-is.
/// </param>
public readonly record struct XrefImport(string Source, string BaseUrl)
{
    /// <summary>Initializes a new instance of the <see cref="XrefImport"/> struct without a base-URL override.</summary>
    /// <param name="source">Local path or URL.</param>
    public XrefImport(string source)
        : this(source, string.Empty)
    {
    }
}
