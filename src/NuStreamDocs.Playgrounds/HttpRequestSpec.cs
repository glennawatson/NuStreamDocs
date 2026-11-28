// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Playgrounds;

/// <summary>Parsed representation of an <c>http-request</c> fenced specification.</summary>
/// <param name="Method">Uppercase HTTP method bytes (such as <c>GET</c> or <c>POST</c>).</param>
/// <param name="Url">Target endpoint URL bytes.</param>
/// <param name="Headers">Parsed <c>Header: Value</c> pairs.</param>
/// <param name="Body">Optional UTF-8 request body bytes.</param>
[System.Diagnostics.DebuggerDisplay("HttpRequestSpec: {Method.Length} bytes")]
internal readonly record struct HttpRequestSpec(
    byte[] Method,
    byte[] Url,
    (byte[] Name, byte[] Value)[] Headers,
    byte[] Body);
