// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SourceDocParser;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Escape hatch: hand the generator a fully-built
/// <see cref="IAssemblySource"/>. Useful for custom acquisition shapes
/// (private feeds, build-output trees, in-memory test fixtures) the
/// other inputs don't cover.
/// </summary>
/// <param name="Source">The caller-built source.</param>
public sealed record CustomInput(IAssemblySource Source) : CSharpApiGeneratorInput;
