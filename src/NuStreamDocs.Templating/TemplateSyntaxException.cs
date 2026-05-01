// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Templating;

/// <summary>
/// Thrown by <see cref="Template.Compile"/> when a template's syntax is malformed.
/// </summary>
public sealed class TemplateSyntaxException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TemplateSyntaxException"/> class.</summary>
    public TemplateSyntaxException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TemplateSyntaxException"/> class.</summary>
    /// <param name="message">Error description.</param>
    public TemplateSyntaxException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TemplateSyntaxException"/> class.</summary>
    /// <param name="message">Error description.</param>
    /// <param name="innerException">Wrapped exception.</param>
    public TemplateSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TemplateSyntaxException"/> class.</summary>
    /// <param name="message">Error description.</param>
    /// <param name="byteOffset">Byte offset into the template source where the error was detected.</param>
    public TemplateSyntaxException(string message, int byteOffset)
        : base(message) =>
        ByteOffset = byteOffset;

    /// <summary>Initializes a new instance of the <see cref="TemplateSyntaxException"/> class with a wrapped cause.</summary>
    /// <param name="message">Error description.</param>
    /// <param name="byteOffset">Byte offset into the template source where the error was detected.</param>
    /// <param name="innerException">Wrapped exception.</param>
    public TemplateSyntaxException(string message, int byteOffset, Exception innerException)
        : base(message, innerException) =>
        ByteOffset = byteOffset;

    /// <summary>Gets the byte offset into the template source where the error was detected.</summary>
    public int ByteOffset { get; }
}
