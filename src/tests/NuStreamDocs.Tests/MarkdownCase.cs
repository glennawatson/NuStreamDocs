// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace NuStreamDocs.Tests;

/// <summary>One Markdown input and the HTML it renders to.</summary>
/// <param name="Markdown">Source text.</param>
/// <param name="Expected">Expected HTML.</param>
[DebuggerDisplay("{Markdown}")]
public sealed record MarkdownCase(string Markdown, string Expected);
