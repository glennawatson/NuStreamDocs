// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts;

/// <summary>Thrown when a font file or stylesheet can't be fetched (network failure, HTTP error, or an offline-mode cache miss).</summary>
public sealed class FontDownloadException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FontDownloadException"/> class.</summary>
    public FontDownloadException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontDownloadException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public FontDownloadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontDownloadException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying failure.</param>
    public FontDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
