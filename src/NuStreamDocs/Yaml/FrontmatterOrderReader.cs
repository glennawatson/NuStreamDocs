// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Yaml;

/// <summary>
/// Reads a page's front-matter <c>Order:</c> integer (Statiq convention) without parsing the
/// whole document. Drives nav-tree ordering: pages and sections with an explicit
/// <c>Order:</c> sort by that value first, then fall back to alphabetical.
/// </summary>
public static class FrontmatterOrderReader
{
    /// <summary>Maximum bytes read from the head of each file.</summary>
    private const int FrontmatterPeekBytes = 1024;

    /// <summary>Tries to read the <c>Order:</c> integer from the front-matter of <paramref name="absolutePath"/>.</summary>
    /// <param name="absolutePath">Absolute path to a markdown page.</param>
    /// <param name="order">The parsed integer on success.</param>
    /// <returns>True when an integer <c>Order:</c> (or lower-cased <c>order:</c>) value is present and parsed cleanly.</returns>
    public static bool TryRead(FilePath absolutePath, out int order)
    {
        order = 0;
        if (absolutePath.IsEmpty)
        {
            return false;
        }

        try
        {
            using var handle = File.OpenHandle(absolutePath);
            var size = (int)Math.Min(FrontmatterPeekBytes, RandomAccess.GetLength(handle));
            if (size <= 0)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[FrontmatterPeekBytes];
            var read = RandomAccess.Read(handle, buffer[..size], 0);
            var scalar = FrontmatterValueExtractor.GetScalar(buffer[..read], "Order"u8);
            if (scalar.IsEmpty)
            {
                scalar = FrontmatterValueExtractor.GetScalar(buffer[..read], "order"u8);
            }

            if (scalar.IsEmpty)
            {
                return false;
            }

            return Utf8IntParser.TryParseInt(scalar, out order);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
