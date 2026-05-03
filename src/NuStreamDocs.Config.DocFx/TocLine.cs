// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.DocFx;

/// <summary>One line decoded from a docfx toc.yml — indent column, key kind, and value bytes.</summary>
/// <remarks>
/// Plain ref struct rather than record struct: holds <see cref="ReadOnlySpan{T}"/> fields, so
/// must live on the stack. Lifetime is bounded by <see cref="TocLineParser"/>.
/// </remarks>
internal readonly ref struct TocLine
{
    /// <summary>Initializes a new instance of the <see cref="TocLine"/> struct.</summary>
    /// <param name="indent">Leading-space column.</param>
    /// <param name="isSequenceItem">True when the line begins with the <c>- </c> marker.</param>
    /// <param name="keyKind">Recognized key.</param>
    /// <param name="value">UTF-8 value bytes (trimmed, dequoted).</param>
    /// <param name="hasItemsKey">True when this line is the <c>items:</c> opener for an inline sub-sequence.</param>
    public TocLine(int indent, bool isSequenceItem, TocKey keyKind, ReadOnlySpan<byte> value, bool hasItemsKey)
    {
        Indent = indent;
        IsSequenceItem = isSequenceItem;
        KeyKind = keyKind;
        Value = value;
        HasItemsKey = hasItemsKey;
    }

    /// <summary>Gets the leading-space column.</summary>
    public int Indent { get; }

    /// <summary>Gets a value indicating whether the line opens a new sequence item (<c>- ...</c>).</summary>
    public bool IsSequenceItem { get; }

    /// <summary>Gets the recognized key kind.</summary>
    public TocKey KeyKind { get; }

    /// <summary>Gets the UTF-8 value bytes (trimmed and dequoted).</summary>
    public ReadOnlySpan<byte> Value { get; }

    /// <summary>Gets a value indicating whether this line is an <c>items:</c> opener for an inline sub-sequence.</summary>
    public bool HasItemsKey { get; }
}
