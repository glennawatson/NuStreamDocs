// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Lightweight navigation metadata for a synthetic page. Discovery plugins that emit pages the
/// regular nav walk can't see (it walks the source folder, synthetic pages live in memory) push
/// one of these so the nav tree can place the page without retaining its body.
/// </summary>
/// <param name="RelativePath">Forward-slashed path relative to the input root (e.g. <c>api/index.md</c>); matches the <see cref="SyntheticPage.RelativePath"/> of the page this describes.</param>
/// <param name="Title">UTF-8 display title; <see langword="null"/> or empty falls back to the path stem.</param>
/// <param name="Order">Optional <c>Order:</c> sort key; <see langword="null"/> sorts the entry after explicitly-ordered siblings.</param>
/// <param name="Hidden">When true the entry (and any section it would otherwise create) is omitted from the nav.</param>
public readonly record struct SyntheticNavEntry(FilePath RelativePath, byte[]? Title, int? Order, bool Hidden);
