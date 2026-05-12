// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader;

/// <summary>One remote Markdown document to pull in, paired with the route it should be served at.</summary>
/// <param name="Url">Absolute URL of the raw Markdown (for example a <c>raw.githubusercontent.com</c> link).</param>
/// <param name="RoutePath">Forward-slashed path relative to the input root that the document is served at (e.g. <c>guide/setup.md</c>).</param>
public readonly record struct RawDocumentEntry(UrlPath Url, FilePath RoutePath);
