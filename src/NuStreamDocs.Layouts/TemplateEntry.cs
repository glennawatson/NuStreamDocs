// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Layouts;

/// <summary>Cached parse of a single layout template plus the absolute path it was loaded from.</summary>
/// <param name="Unit">Parsed template bytes and token stream.</param>
/// <param name="ResolvedPath">Absolute path the template was loaded from (diagnostics only).</param>
/// <remarks>
/// Holds the template bytes, the parsed <see cref="TemplateUnit"/> (whose
/// token offsets index into those bytes), and the resolved absolute path
/// used for missing-file diagnostics. No per-page state is captured —
/// the same entry is reusable across every page that references the
/// template.
/// </remarks>
internal sealed record TemplateEntry(TemplateUnit Unit, ApiCompatString ResolvedPath);
