// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Redirects;

/// <summary>One redirect: requests for <paramref name="From"/> are sent to <paramref name="To"/>.</summary>
/// <param name="From">UTF-8 root-relative URL path the redirect is from (e.g. <c>/old/page/</c>).</param>
/// <param name="To">UTF-8 destination — a root-relative URL path or an absolute <c>https://…</c> URL.</param>
/// <param name="Permanent">When true the redirect is a 301; otherwise a 302.</param>
public readonly record struct RedirectRule(byte[] From, byte[] To, bool Permanent);
