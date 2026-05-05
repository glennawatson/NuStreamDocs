// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>One slot in the flat <see cref="NavTree"/> array consumed by <see cref="NavRenderer"/>.</summary>
/// <param name="Title">UTF-8 display title bytes; encoded once at <see cref="NavTreeFlattener"/> time.</param>
/// <param name="RelativePath">Source-relative path (file) or directory path (section).</param>
/// <param name="IndexPath">Source-relative path of the section's promoted index page; empty when none.</param>
/// <param name="RelativeUrlBytes">Pre-encoded served URL bytes derived from <see cref="RelativePath"/>.</param>
/// <param name="IndexUrlBytes">Pre-encoded served URL bytes derived from <see cref="IndexPath"/>.</param>
/// <param name="ParentIndex">Index of this node's parent in <see cref="NavTree.Nodes"/>; <c>-1</c> for the root.</param>
/// <param name="FirstChildIndex">Index of this node's first child; <c>-1</c> when there are no children.</param>
/// <param name="ChildCount">Number of contiguous children at <see cref="FirstChildIndex"/>.</param>
/// <param name="IsSection">True when this node is a section rather than a page.</param>
internal readonly record struct NavTreeNode(
    byte[] Title,
    FilePath RelativePath,
    FilePath IndexPath,
    byte[] RelativeUrlBytes,
    byte[] IndexUrlBytes,
    int ParentIndex,
    int FirstChildIndex,
    int ChildCount,
    bool IsSection);
