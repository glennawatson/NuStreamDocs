// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Keys;

/// <summary>A single resolved key alias.</summary>
/// <param name="ClassSuffix">Class-name suffix used in <c>key-<i>suffix</i></c>.</param>
/// <param name="Label">Display label rendered inside the <c>kbd</c>.</param>
internal readonly record struct KeyEntry(string ClassSuffix, string Label);
