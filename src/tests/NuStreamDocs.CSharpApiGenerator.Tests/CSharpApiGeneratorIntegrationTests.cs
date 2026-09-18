// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using SourceDocParser;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Exercises metadata extraction and Markdown emission against a compiled local assembly.</summary>
public sealed class CSharpApiGeneratorIntegrationTests
{
    /// <summary>A public type can be extracted and rendered through the installed parser packages.</summary>
    /// <param name="cancellationToken">Cancellation token supplied by the test runner.</param>
    /// <returns>The asynchronous integration test.</returns>
    [Test]
    public async Task ExtractAndGenerateLocalAssembly(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var assemblyPath = Path.Combine(directory.FullName, "IntegrationFixture.dll");
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            var runtimeAssembly = Path.Combine(runtimeDirectory, "System.Private.CoreLib.dll");
            var syntax = CSharpSyntaxTree.ParseText(
                """
                namespace Fixture;
                /// <summary>A documented type for the generator integration test.</summary>
                public sealed class Widget
                {
                    /// <summary>Returns the supplied value.</summary>
                    public int Echo(int value) => value;
                }
                """,
                cancellationToken: cancellationToken);
            var compilation = CSharpCompilation.Create(
                "IntegrationFixture",
                [syntax],
                [MetadataReference.CreateFromFile(runtimeAssembly)],
                new(OutputKind.DynamicallyLinkedLibrary));
            await using var assembly = new MemoryStream();
            await using var documentation = new MemoryStream();
            var emitted = compilation.Emit(assembly, xmlDocumentationStream: documentation, cancellationToken: cancellationToken);
            await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
            await File.WriteAllBytesAsync(assemblyPath, assembly.ToArray(), cancellationToken);
            await File.WriteAllBytesAsync(Path.ChangeExtension(assemblyPath, ".xml"), documentation.ToArray(), cancellationToken);

            var options = CSharpApiGeneratorOptions.From(new LocalAssembliesInput(
                $"net{Environment.Version.Major}.0",
                [assemblyPath],
                [runtimeDirectory]));
            var extraction = await CSharpApiGenerator.ExtractAsync(options, NullLogger.Instance, cancellationToken);
            await Assert.That(extraction.CanonicalTypes).HasSingleItem();
            await Assert.That(extraction.CanonicalTypes[0].FullName).IsEqualTo("Fixture.Widget");

            var pages = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
            var sink = new CallbackPageSink((path, bytes) => pages[path] = bytes);
            var generated = await CSharpApiGenerator.GenerateAsync(options, sink, NullLogger.Instance, cancellationToken);
            await Assert.That(generated.CanonicalTypes).IsEqualTo(1);
            await Assert.That(generated.PagesEmitted).IsGreaterThan(0);
            await Assert.That(pages).IsNotEmpty();
            var containsType = false;
            foreach (var (_, page) in pages)
            {
                containsType |= Encoding.UTF8.GetString(page).Contains("Widget", StringComparison.Ordinal);
            }

            await Assert.That(containsType).IsTrue();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
