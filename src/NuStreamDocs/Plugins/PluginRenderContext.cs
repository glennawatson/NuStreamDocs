// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Per-page context handed to <see cref="IDocPlugin.OnRenderPageAsync"/>.
/// </summary>
/// <param name="RelativePath">Page path relative to the input root, e.g. <c>guide/intro.md</c>.</param>
/// <param name="Source">UTF-8 markdown source bytes.</param>
/// <param name="Html">UTF-8 HTML output buffer; plugins may rewrite by
/// inspecting <see cref="ArrayBufferWriter{T}.WrittenSpan"/> and
/// reassembling into a fresh writer they swap in via the build pipeline.</param>
public readonly record struct PluginRenderContext(
    FilePath RelativePath,
    ReadOnlyMemory<byte> Source,
    ArrayBufferWriter<byte> Html);
