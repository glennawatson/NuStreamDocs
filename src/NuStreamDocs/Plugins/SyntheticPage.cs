// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>One in-memory markdown page registered by a discovery-phase plugin so it flows through the regular render pipeline without ever touching the source folder.</summary>
/// <param name="RelativePath">Forward-slashed path relative to the input root (e.g. <c>tags/index.md</c>); decides the output URL the same way disk-loaded pages do.</param>
/// <param name="MarkdownBytes">UTF-8 markdown source (frontmatter + body).</param>
public readonly record struct SyntheticPage(FilePath RelativePath, byte[] MarkdownBytes);
