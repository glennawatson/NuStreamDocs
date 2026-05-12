// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.ContentLoader;

/// <summary>Thrown when a content loader cannot fetch or parse its source.</summary>
public sealed class ContentLoaderException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ContentLoaderException"/> class.</summary>
    public ContentLoaderException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ContentLoaderException"/> class.</summary>
    /// <param name="message">Error message.</param>
    public ContentLoaderException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ContentLoaderException"/> class.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Underlying cause.</param>
    public ContentLoaderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
