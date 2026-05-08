// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>One entry from a <c>nav:</c> list — a path plus an optional display-title override.</summary>
/// <param name="Path">Path bytes (filename or subsection directory).</param>
/// <param name="Title">Display-title override bytes; empty when the entry uses the bare-path form.</param>
internal readonly record struct PagesEntry(byte[] Path, byte[] Title);
