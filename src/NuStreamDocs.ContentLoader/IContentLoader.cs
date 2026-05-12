// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader;

/// <summary>
/// Produces in-memory Markdown pages from a source other than the local input folder — a structured
/// data file, a remote API, another repository, and so on. Loaded pages flow through the normal
/// render pipeline as <see cref="SyntheticPage"/> entries.
/// </summary>
public interface IContentLoader
{
    /// <summary>Gets a short stable identifier for this loader, used in build diagnostics.</summary>
    ReadOnlySpan<byte> Name { get; }

    /// <summary>Loads the source and produces the synthetic pages it contributes.</summary>
    /// <param name="context">Per-build context (input root, URL style).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pages this loader contributes; empty when the source has nothing.</returns>
    ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken);
}
