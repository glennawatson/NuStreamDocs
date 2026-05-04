// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Templating;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Loaded page shell plus compiled partials and bundled static assets.
/// </summary>
public interface IThemePackage
{
    /// <summary>Gets the compiled top-level page template.</summary>
    Template Page { get; }

    /// <summary>Gets the compiled partial registry keyed by UTF-8 partial-name bytes.</summary>
    /// <remarks>The renderer probes via the byte-keyed alternate-lookup pattern so per-partial dispatch never allocates a string.</remarks>
    Dictionary<byte[], Template> Partials { get; }

    /// <summary>Gets the static assets as an indexable snapshot for write-out loops.</summary>
    (FilePath RelativePath, byte[] Bytes)[] StaticAssetEntries { get; }
}
