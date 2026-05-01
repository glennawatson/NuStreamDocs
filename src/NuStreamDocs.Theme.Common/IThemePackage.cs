// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using NuStreamDocs.Templating;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Loaded page shell plus compiled partials and bundled static assets.
/// </summary>
public interface IThemePackage
{
    /// <summary>Gets the compiled top-level page template.</summary>
    Template Page { get; }

    /// <summary>Gets the compiled partial registry keyed by partial name.</summary>
    FrozenDictionary<string, Template> Partials { get; }

    /// <summary>Gets the static assets as an indexable snapshot for write-out loops.</summary>
    (string RelativePath, byte[] Bytes)[] StaticAssetEntries { get; }
}
