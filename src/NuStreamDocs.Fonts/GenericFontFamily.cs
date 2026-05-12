// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts;

/// <summary>The generic CSS font family a declared face falls back to — also selects the system reference font used to compute the CLS metric overrides.</summary>
public enum GenericFontFamily
{
    /// <summary><c>sans-serif</c>; reference font Arial.</summary>
    SansSerif,

    /// <summary><c>serif</c>; reference font Times New Roman.</summary>
    Serif,

    /// <summary><c>monospace</c>; reference font Courier New.</summary>
    Monospace,
}
