// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Layouts;

/// <summary>Configuration for <see cref="LayoutsPlugin"/>.</summary>
/// <param name="TemplateDirectory">Directory the plugin reads layout files from when a page's frontmatter requests one via <c>template:</c>.</param>
/// <param name="MaxIncludeDepth">Upper bound on nested <c>{% include %}</c> / <c>{% extends %}</c> expansion before the renderer stops recursing and logs a warning.</param>
public sealed record LayoutsOptions(
    DirectoryPath TemplateDirectory,
    int MaxIncludeDepth)
{
    /// <summary>Default cap for nested <c>{% include %}</c> / <c>{% extends %}</c> expansion when callers do not override it.</summary>
    private const int DefaultDepth = 8;

    /// <summary>Gets the default include / extends recursion cap.</summary>
    public static int DefaultMaxIncludeDepth => DefaultDepth;

    /// <summary>Gets the default option set — empty template directory, default depth cap.</summary>
    public static LayoutsOptions Default { get; } = new(
        default,
        DefaultDepth);
}
