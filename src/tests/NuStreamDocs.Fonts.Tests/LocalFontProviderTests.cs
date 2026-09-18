// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="LocalFontProvider"/>.</summary>
public class LocalFontProviderTests
{
    /// <summary>Directory holding the fixture fonts.</summary>
    private const string FontDirectory = "fonts";

    /// <summary>Expected number of resolved font files.</summary>
    private const int ExpectedCount = 2;

    /// <summary>Gets the expected family.</summary>
    private static ReadOnlySpan<byte> FamilyBytes => "MyFont"u8;

    /// <summary>A glob matching two files yields two resources; the italic one is detected from its filename.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolvesGlobbedFiles()
    {
        using TempDir dir = new();
        _ = Directory.CreateDirectory(Path.Combine(dir.Root, FontDirectory));
        byte[] regularBytes = [1, 2, 3];
        byte[] italicBytes = [4, 5, 6, 7];
        await File.WriteAllBytesAsync(Path.Combine(dir.Root, FontDirectory, "MyFont-Regular.woff2"), regularBytes);
        await File.WriteAllBytesAsync(Path.Combine(dir.Root, FontDirectory, "MyFont-Italic.woff2"), italicBytes);

        var face = FontsOptions.Default.AddLocalFont(FamilyBytes, "fonts/MyFont-*.woff2").Faces[0];
        var resources = await LocalFontProvider.Instance.ResolveAsync(
            face,
            [],
            new(dir.Root, true),
            dir.Root,
            null,
            CancellationToken.None);

        await Assert.That(resources.Length).IsEqualTo(ExpectedCount);
        var italic = Array.Find(resources, static r => r.Style == FontStyle.Italic);
        await Assert.That(italic.Woff2Bytes.SequenceEqual(italicBytes)).IsTrue();
        var regular = Array.Find(resources, static r => r.Style == FontStyle.Normal);
        await Assert.That(regular.Woff2Bytes.SequenceEqual(regularBytes)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(regular.FamilyBytes)).IsEqualTo("MyFont");
    }

    /// <summary>No match throws <see cref="FontDownloadException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoMatchThrows()
    {
        using TempDir dir = new();
        var face = FontsOptions.Default.AddLocalFont(FamilyBytes, "fonts/*.woff2").Faces[0];
        var threw = false;
        try
        {
            await LocalFontProvider.Instance.ResolveAsync(
                face,
                [],
                new(dir.Root, true),
                dir.Root,
                null,
                CancellationToken.None);
        }
        catch (FontDownloadException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }
}
