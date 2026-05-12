// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Redirects;

/// <summary>One block of the <c>_headers</c> file: HTTP headers applied to paths matching a pattern.</summary>
/// <param name="PathPattern">UTF-8 path pattern in the Netlify / Cloudflare-Pages syntax (e.g. <c>/assets/*</c>, <c>/blog/:slug</c>).</param>
/// <param name="HeaderLines">UTF-8 header lines in <c>Name: value</c> form (e.g. <c>Cache-Control: public, max-age=31536000, immutable</c>).</param>
public readonly record struct HeaderRule(byte[] PathPattern, byte[][] HeaderLines);
