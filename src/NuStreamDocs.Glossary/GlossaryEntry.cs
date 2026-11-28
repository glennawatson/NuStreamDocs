// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Glossary;

/// <summary>Represents one central glossary entry with its UTF-8 term, definition, and anchor slug.</summary>
/// <param name="Term">Display term bytes.</param>
/// <param name="Definition">Plain-text definition bytes.</param>
/// <param name="Slug">Lowercase anchor slug bytes.</param>
[System.Diagnostics.DebuggerDisplay("GlossaryEntry: {Term.Length} bytes")]
public readonly record struct GlossaryEntry(byte[] Term, byte[] Definition, byte[] Slug);
