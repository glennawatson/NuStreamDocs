// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Xrefs;

/// <summary>Decoded xrefmap document.</summary>
/// <param name="BaseUrl">Document's <c>baseUrl</c> field (empty when absent).</param>
/// <param name="Entries">Parsed <c>(uid, href)</c> pairs in source order; entries missing either field are dropped.</param>
internal readonly record struct XrefMapPayload(string BaseUrl, (string Uid, string Href)[] Entries);
