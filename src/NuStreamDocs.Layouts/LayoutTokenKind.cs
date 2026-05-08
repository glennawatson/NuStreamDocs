// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Layouts;

/// <summary>Token shape emitted by <see cref="LayoutScanner"/>.</summary>
internal enum LayoutTokenKind
{
    /// <summary>Literal HTML bytes; copy through verbatim.</summary>
    Literal,

    /// <summary>A <c>{{ page.X }}</c> reference; payload is the bare name (the <c>X</c>).</summary>
    Variable,

    /// <summary>A <c>{{ super() }}</c> reference.</summary>
    Super,

    /// <summary>A <c>{% extends "Y" %}</c> tag; payload is the unquoted target name.</summary>
    Extends,

    /// <summary>A <c>{% block name %}</c> opener; payload is the bare block name.</summary>
    BlockOpen,

    /// <summary>A <c>{% endblock %}</c> closer.</summary>
    BlockClose,

    /// <summary>A <c>{% include "Z" %}</c> tag; payload is the unquoted target name.</summary>
    Include,

    /// <summary>An unsupported tag; payload is the raw tag body (between <c>{%</c> and <c>%}</c>, trimmed).</summary>
    Unsupported,

    /// <summary>An unterminated marker that did not close — emitted as a literal so the renderer copies the source bytes through.</summary>
    Malformed,
}
