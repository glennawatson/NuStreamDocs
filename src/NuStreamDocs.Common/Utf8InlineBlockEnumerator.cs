// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Walks UTF-8 HTML and yields the body of every <c>{open}…&gt;…{close}</c> block — for example
/// inline <c>&lt;script&gt;</c> / <c>&lt;style&gt;</c> elements. Tolerant scanner, not a full HTML parser.
/// </summary>
public ref struct Utf8InlineBlockEnumerator
{
    /// <summary>ASCII byte for the closing angle bracket of the opening tag.</summary>
    private const byte CloseAngle = (byte)'>';

    /// <summary>Page HTML being scanned.</summary>
    private readonly ReadOnlySpan<byte> _html;

    /// <summary>Opening-tag prefix to search for (e.g. <c>"&lt;style"u8</c>).</summary>
    private readonly ReadOnlySpan<byte> _open;

    /// <summary>Closing tag that terminates a block body (e.g. <c>"&lt;/style&gt;"u8</c>).</summary>
    private readonly ReadOnlySpan<byte> _close;

    /// <summary>Offset into <see cref="_html"/> where the next search starts.</summary>
    private int _cursor;

    /// <summary>Initializes a new instance of the <see cref="Utf8InlineBlockEnumerator"/> struct over <paramref name="html"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="openTagPrefix">Opening-tag prefix (e.g. <c>"&lt;script"u8</c>).</param>
    /// <param name="closeTag">Closing tag (e.g. <c>"&lt;/script&gt;"u8</c>).</param>
    public Utf8InlineBlockEnumerator(
        ReadOnlySpan<byte> html,
        ReadOnlySpan<byte> openTagPrefix,
        ReadOnlySpan<byte> closeTag)
    {
        _html = html;
        _open = openTagPrefix;
        _close = closeTag;
    }

    /// <summary>Gets the current block body span (the bytes between the opening tag's <c>&gt;</c> and the closing tag). May be empty.</summary>
    public ReadOnlySpan<byte> Current { get; private set; }

    /// <summary>Returns this enumerator so it can drive a <c>foreach</c>.</summary>
    /// <returns>This enumerator.</returns>
    public readonly Utf8InlineBlockEnumerator GetEnumerator() => this;

    /// <summary>Advances to the next block body.</summary>
    /// <returns>True when another block was found.</returns>
    public bool MoveNext()
    {
        if (_cursor >= _html.Length)
        {
            return false;
        }

        var rel = _html[_cursor..].IndexOf(_open);
        if (rel < 0)
        {
            _cursor = _html.Length;
            return false;
        }

        // Skip past the opening-tag attributes to its closing '>'.
        var tagOpenStart = _cursor + rel;
        var afterOpen = _html[(tagOpenStart + _open.Length)..];
        var tagCloseRel = afterOpen.IndexOf(CloseAngle);
        if (tagCloseRel < 0)
        {
            _cursor = _html.Length;
            return false;
        }

        var bodyStart = tagOpenStart + _open.Length + tagCloseRel + 1;
        var endRel = _html[bodyStart..].IndexOf(_close);
        if (endRel < 0)
        {
            _cursor = _html.Length;
            return false;
        }

        Current = _html.Slice(bodyStart, endRel);
        _cursor = bodyStart + endRel + _close.Length;
        return true;
    }
}
