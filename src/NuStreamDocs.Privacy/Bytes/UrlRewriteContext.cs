// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>Read-only context bundle passed down to URL byte rewriters so each scanner takes a single parameter.</summary>
/// <param name="Filter">Host filter that decides which URLs to localise.</param>
/// <param name="Registry">URL registry that returns the local rewrite path.</param>
internal readonly record struct UrlRewriteContext(HostFilter Filter, ExternalAssetRegistry Registry);
