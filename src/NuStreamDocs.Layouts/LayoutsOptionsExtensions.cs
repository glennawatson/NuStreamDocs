// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Layouts;

/// <summary>Construction helpers for <see cref="LayoutsOptions"/>.</summary>
public static class LayoutsOptionsExtensions
{
    /// <summary>Extension members for <c>LayoutsOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(LayoutsOptions options)
    {
        /// <summary>Returns a copy of <paramref name="options"/> with <see cref="LayoutsOptions.TemplateDirectory"/> set.</summary>
        /// <param name="templateDirectory">Directory layouts are loaded from.</param>
        /// <returns>The updated options.</returns>
        public LayoutsOptions
            WithTemplateDirectory(in DirectoryPath templateDirectory) =>
            options with { TemplateDirectory = templateDirectory };

        /// <summary>Returns a copy of <paramref name="options"/> with <see cref="LayoutsOptions.MaxIncludeDepth"/> set.</summary>
        /// <param name="maxIncludeDepth">Nested-include cap; must be at least <c>1</c>.</param>
        /// <returns>The updated options.</returns>
        public LayoutsOptions WithMaxIncludeDepth(int maxIncludeDepth)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxIncludeDepth, 1);
            return options with { MaxIncludeDepth = maxIncludeDepth };
        }
    }
}
