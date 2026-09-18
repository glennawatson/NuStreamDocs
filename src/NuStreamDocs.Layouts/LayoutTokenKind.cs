// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Layouts;

/// <summary>Token shape emitted by <see cref="LayoutScanner"/>.</summary>
internal enum LayoutTokenKind
{
    /// <summary>Literal HTML bytes; copy through verbatim.</summary>
    Literal = 0,

    /// <summary>A <c>{{ page.X }}</c> reference; payload is the bare name (the <c>X</c>).</summary>
    Variable = 1,

    /// <summary>A <c>{{ super() }}</c> reference.</summary>
    Super = 2,

    /// <summary>A <c>{% extends "Y" %}</c> tag; payload is the unquoted target name.</summary>
    Extends = 3,

    /// <summary>A <c>{% block name %}</c> opener; payload is the bare block name.</summary>
    BlockOpen = 4,

    /// <summary>A <c>{% endblock %}</c> closer.</summary>
    BlockClose = 5,

    /// <summary>A <c>{% include "Z" %}</c> tag; payload is the unquoted target name.</summary>
    Include = 6,

    /// <summary>An unsupported tag; payload is the raw tag body (between <c>{%</c> and <c>%}</c>, trimmed).</summary>
    Unsupported = 7,

    /// <summary>An unterminated marker that did not close — emitted as a literal so the renderer copies the source bytes through.</summary>
    Malformed = 8,
}
