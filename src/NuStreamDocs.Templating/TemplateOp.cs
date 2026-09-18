// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Templating;

/// <summary>Opcode in a compiled <see cref="Template"/> instruction stream.</summary>
public enum TemplateOp
{
    /// <summary>Verbatim slice of the template source.</summary>
    Literal = 0,

    /// <summary>HTML-escaped variable substitution (<c>{{name}}</c>).</summary>
    EscapedVariable = 1,

    /// <summary>Unescaped variable substitution (<c>{{{name}}}</c> or <c>{{&amp;name}}</c>).</summary>
    RawVariable = 2,

    /// <summary>Truthy section open (<c>{{#name}}</c>).</summary>
    SectionOpen = 3,

    /// <summary>Inverted section open (<c>{{^name}}</c>).</summary>
    InvertedSectionOpen = 4,

    /// <summary>Section close (<c>{{/name}}</c>).</summary>
    SectionClose = 5,

    /// <summary>Partial inclusion (<c>{{&gt; name}}</c>).</summary>
    Partial = 6,
}
