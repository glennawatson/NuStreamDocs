// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Macros.Logging;

/// <summary>Source-generated logging entry points for <see cref="MacrosPlugin"/>.</summary>
internal static partial class MacrosLoggingHelper
{
    /// <summary>Logs a single unresolved <c>{{ name }}</c>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="name">Unresolved name.</param>
    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Warning,
        Message = "Macros: no value for {{ {Name} }}; left in place")]
    public static partial void LogMissingVariable(ILogger logger, string name);
}
