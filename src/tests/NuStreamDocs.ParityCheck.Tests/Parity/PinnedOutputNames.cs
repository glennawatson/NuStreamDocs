// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Names shared by the generated pinned-output tests and the check that keeps them in step with the sidecars.</summary>
internal static class PinnedOutputNames
{
    /// <summary>The name of the generated test class.</summary>
    internal const string ClassName = "SidecarPinnedOutputTests";

    /// <summary>The name of the generated source file.</summary>
    internal const string FileName = "SidecarPinnedOutputTests.g.cs";

    /// <summary>Gets the name of the generated test method for a sidecar kind.</summary>
    /// <param name="kind">One of <see cref="ExpectationKinds.Pinned"/>.</param>
    /// <returns>The method name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no pinned-output test.</exception>
    internal static string MethodName(string kind) =>
        kind switch
        {
            ExpectationKinds.Deviation => "DeviationRendersPinnedHtml",
            ExpectationKinds.Extension => "ExtensionRendersPinnedHtml",
            ExpectationKinds.MkDocsBetter => "MkDocsBetterRendersPinnedHtml",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The sidecar kind has no pinned-output test."),
        };

    /// <summary>Gets the display name of the generated test row for a fragment.</summary>
    /// <param name="id">Fragment id.</param>
    /// <param name="expectation">The fragment's sidecar.</param>
    /// <returns>The id, kind and reason of the sidecar.</returns>
    internal static string DisplayName(string id, Expectation expectation) => $"{id} - {expectation.Kind}: {expectation.Reason}";
}
