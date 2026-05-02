// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using NuStreamDocs.Autorefs;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SphinxInventory.Tests;

/// <summary>End-to-end coverage for the Sphinx <c>objects.inv</c> writer.</summary>
public class SphinxInventoryWriterTests
{
    /// <summary>The header is plain UTF-8 with the four canonical lines.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeaderIsCanonicalSphinxV2()
    {
        using var fixture = new InventoryFixture();
        SphinxInventoryWriter.Write(fixture.Path, new("MyDocs", "1.2.3", "objects.inv"), []);
        var bytes = await File.ReadAllBytesAsync(fixture.Path);
        var header = ReadHeaderText(bytes, out _);
        await Assert.That(header).IsEqualTo(
            "# Sphinx inventory version 2\n# Project: MyDocs\n# Version: 1.2.3\n# The remainder of this file is compressed using zlib.\n");
    }

    /// <summary>Body is zlib-compressed and decompresses to per-entry lines in the documented format.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BodyDecompressesToEntryLines()
    {
        using var fixture = new InventoryFixture();
        var entries = new (string Id, string Url)[]
        {
            ("MyType", "api/MyType.html"),
            ("MyType.Method", "api/MyType.html#method"),
        };
        SphinxInventoryWriter.Write(fixture.Path, SphinxInventoryOptions.Default, entries);

        var bytes = await File.ReadAllBytesAsync(fixture.Path);
        _ = ReadHeaderText(bytes, out var bodyOffset);
        var body = DecompressBody(bytes, bodyOffset);
        await Assert.That(body).IsEqualTo(
            "MyType std:label -1 api/MyType.html -\nMyType.Method std:label -1 api/MyType.html#method -\n");
    }

    /// <summary>An empty registry still produces a valid (header-only + empty zlib) inventory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyRegistryProducesValidFile()
    {
        using var fixture = new InventoryFixture();
        SphinxInventoryWriter.Write(fixture.Path, SphinxInventoryOptions.Default, []);
        var bytes = await File.ReadAllBytesAsync(fixture.Path);
        _ = ReadHeaderText(bytes, out var bodyOffset);
        var body = DecompressBody(bytes, bodyOffset);
        await Assert.That(body).IsEqualTo(string.Empty);
    }

    /// <summary>Plugin emits the file at finalize time using the shared registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PluginEmitsFileAtFinalize()
    {
        using var fixture = new InventoryFixture();
        var registry = new AutorefsRegistry();
        registry.Register("Foo", "api/Foo.html", fragment: null);
        var plugin = new SphinxInventoryPlugin(registry, new("X", string.Empty, "objects.inv"));
        var context = new PluginFinalizeContext(fixture.Directory);
        await plugin.OnFinalizeAsync(context, CancellationToken.None);

        var path = Path.Combine(fixture.Directory, "objects.inv");
        await Assert.That(File.Exists(path)).IsTrue();
        var bytes = await File.ReadAllBytesAsync(path);
        _ = ReadHeaderText(bytes, out var bodyOffset);
        var body = DecompressBody(bytes, bodyOffset);
        await Assert.That(body).IsEqualTo("Foo std:label -1 api/Foo.html -\n");
    }

    /// <summary>UseSphinxInventory(builder) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSphinxInventoryRegistersWithDefault()
    {
        var builder = new DocBuilder();
        await Assert.That(builder.UseSphinxInventory()).IsSameReferenceAs(builder);
    }

    /// <summary>UseSphinxInventory(builder, registry) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSphinxInventoryRegistersWithRegistry()
    {
        var builder = new DocBuilder();
        var registry = new AutorefsRegistry();
        await Assert.That(builder.UseSphinxInventory(registry)).IsSameReferenceAs(builder);
    }

    /// <summary>UseSphinxInventory(builder, registry, options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSphinxInventoryRegistersWithOptions()
    {
        var builder = new DocBuilder();
        var registry = new AutorefsRegistry();
        var options = SphinxInventoryOptions.Default;
        await Assert.That(builder.UseSphinxInventory(registry, options)).IsSameReferenceAs(builder);
    }

    /// <summary>UseSphinxInventory rejects a null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSphinxInventoryRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderSphinxInventoryExtensions.UseSphinxInventory(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Reads the four header lines as UTF-8 text and returns the offset of the first compressed byte.</summary>
    /// <param name="bytes">File bytes.</param>
    /// <param name="bodyOffset">Output: the offset of the first byte after the four header lines.</param>
    /// <returns>The decoded header text.</returns>
    private static string ReadHeaderText(byte[] bytes, out int bodyOffset)
    {
        var newlines = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] is (byte)'\n')
            {
                newlines++;
            }

            if (newlines is not 4)
            {
                continue;
            }

            bodyOffset = i + 1;
            return Encoding.UTF8.GetString(bytes, 0, bodyOffset);
        }

        bodyOffset = bytes.Length;
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Decompresses the zlib-compressed body and returns it as UTF-8 text.</summary>
    /// <param name="bytes">Full file bytes.</param>
    /// <param name="bodyOffset">Offset of the first compressed byte.</param>
    /// <returns>Decoded body text.</returns>
    private static string DecompressBody(byte[] bytes, int bodyOffset)
    {
        using var input = new MemoryStream(bytes, bodyOffset, bytes.Length - bodyOffset);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    /// <summary>Throwaway file under the test temp dir.</summary>
    private sealed class InventoryFixture : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="InventoryFixture"/> class.</summary>
        public InventoryFixture()
        {
            Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "smkd-inv-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            Path = System.IO.Path.Combine(Directory, "objects.inv");
        }

        /// <summary>Gets the temp directory hosting the inventory file.</summary>
        public string Directory { get; }

        /// <summary>Gets the absolute path of the inventory file.</summary>
        public string Path { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return;
            }

            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
