// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Layouts.Logging;

/// <summary>Source-generated logging entry points for <see cref="LayoutsPlugin"/>.</summary>
internal static partial class LayoutsLoggingHelper
{
    /// <summary>Logs a missing layout file at <c>Warning</c>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Resolved layout path that did not exist.</param>
    [LoggerMessage(
        EventId = 9201,
        Level = LogLevel.Warning,
        Message = "Layouts: template not found at {Path}; passing rendered HTML through unchanged")]
    public static partial void LogMissingTemplate(ILogger logger, string path);

    /// <summary>Logs an unresolved <c>{{ page.X }}</c> reference at <c>Warning</c>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="name">Unresolved variable name.</param>
    [LoggerMessage(
        EventId = 9202,
        Level = LogLevel.Warning,
        Message = "Layouts: no value for {{{{ {Name} }}}}; emitting empty string")]
    public static partial void LogMissingVariable(ILogger logger, string name);

    /// <summary>Logs an include-depth overflow at <c>Warning</c>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="depth">Configured maximum depth.</param>
    /// <param name="path">Include path that was refused.</param>
    [LoggerMessage(
        EventId = 9203,
        Level = LogLevel.Warning,
        Message = "Layouts: include depth {Depth} exceeded at {Path}; expansion stopped")]
    public static partial void LogIncludeDepthExceeded(ILogger logger, int depth, string path);

    /// <summary>Logs a missing include-target file at <c>Warning</c>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Resolved include path.</param>
    [LoggerMessage(
        EventId = 9204,
        Level = LogLevel.Warning,
        Message = "Layouts: include target {Path} not found; skipping")]
    public static partial void LogMissingInclude(ILogger logger, string path);

    /// <summary>Logs an unsupported tag at <c>Warning</c>; the tag text is passed through verbatim.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="tag">Tag text (without the surrounding <c>{% %}</c>).</param>
    [LoggerMessage(
        EventId = 9205,
        Level = LogLevel.Warning,
        Message = "Layouts: unsupported tag {{% {Tag} %}}; passed through verbatim")]
    public static partial void LogUnsupportedTag(ILogger logger, string tag);

    /// <summary>Logs a <c>{{ super() }}</c> reference outside an overriding block at <c>Warning</c>.</summary>
    /// <param name="logger">Target logger.</param>
    [LoggerMessage(
        EventId = 9206,
        Level = LogLevel.Warning,
        Message = "Layouts: {{ super() }} called outside an overriding block; emitting empty string")]
    public static partial void LogSuperOutsideBlock(ILogger logger);
}
