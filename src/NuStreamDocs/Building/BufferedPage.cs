// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Building;

/// <summary>One page held back from immediate write because the build needs cross-page resolution.</summary>
/// <param name="RelativePath">Source-relative path.</param>
/// <param name="OutputPath">Absolute output path.</param>
/// <param name="Rental">Pooled rental whose writer holds the post-render HTML; transferred ownership — the drain phase disposes it.</param>
/// <param name="Hash">Source-content xxHash3 digest used as the manifest cache key.</param>
internal readonly record struct BufferedPage(
    FilePath RelativePath,
    FilePath OutputPath,
    PageBuilderRental Rental,
    byte[] Hash);
