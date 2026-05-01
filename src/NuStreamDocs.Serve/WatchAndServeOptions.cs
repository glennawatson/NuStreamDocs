// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Serve;

/// <summary>
/// Options for the watch + serve pipeline (see <see cref="DocBuilderServeExtensions"/>).
/// </summary>
/// <param name="Host">Bind address; defaults to <c>127.0.0.1</c>. Use <c>0.0.0.0</c> for LAN access.</param>
/// <param name="Port">TCP port for the dev server; defaults to <c>8000</c>.</param>
/// <param name="DebounceMs">How long to wait after the last file-system event before triggering a rebuild. Coalesces save bursts that editors and formatters produce.</param>
/// <param name="LiveReload">When true, connected browsers receive a reload signal over a websocket after each successful rebuild. Defaults to <see langword="true"/>.</param>
/// <param name="OpenBrowser">When true, opens the default browser to the served URL on first start. Defaults to <see langword="false"/>.</param>
/// <param name="WatchOutput">When true, ignores file-system events under the output root (the build itself writes there and would re-trigger). Defaults to <see langword="true"/>.</param>
public readonly record struct WatchAndServeOptions(
    string Host,
    int Port,
    int DebounceMs,
    bool LiveReload,
    bool OpenBrowser,
    bool WatchOutput)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static WatchAndServeOptions Default { get; } = new(
        Host: "127.0.0.1",
        Port: 8000,
        DebounceMs: 250,
        LiveReload: true,
        OpenBrowser: false,
        WatchOutput: true);
}
