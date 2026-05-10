// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Signature for a delegate that loads a partial from a given path.
/// </summary>
/// <param name="path">The path to the partial.</param>
/// <returns>The partial's bytes.</returns>'
public delegate byte[] PartialLoader(in FilePath path);
