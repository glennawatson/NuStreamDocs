// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.Schemas.Tests;

/// <summary>Tests for <see cref="JsonSchemaContentLoader"/>, <see cref="JsonSchemaPageBuilder"/>, and <see cref="SchemaSourceHelper"/>.</summary>
public sealed class JsonSchemaContentLoaderTests
{
    /// <summary>Shared route prefix segment used in test cases.</summary>
    private const string RefPrefix = "ref";

    /// <summary>Gets a sample JSON Schema Draft 2020-12 document exercising types, arrays, defaults, enums, and <c>$defs</c>.</summary>
    private static ReadOnlySpan<byte> RichJsonSchema => """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "App \"Core\" \\ Config!",
          "description": "Root configuration object.",
          "type": "object",
          "required": ["host", 42],
          "properties": {
            "host": {
              "type": ["string", null, "null"],
              "default": "localhost|8080\r\nnext",
              "description": "Bind host|port\r\nline.",
              "enum": ["localhost", "0.0.0.0"],
              "extraIgnored": true
            },
            "port": {
              "type": "integer",
              "default": 8080,
              "enum": [80, 8080]
            },
            "tls": {
              "$ref": "#/$defs/TlsSettings",
              "default": false
            },
            "untyped": {
              "default": true
            },
            "skippedBoolProp": true
          },
          "$defs": {
            "TlsSettings": {
              "description": "TLS sub-schema.",
              "type": "object",
              "properties": {
                "certPath": { "type": "string" }
              },
              "$defs": {
                "NestedIgnored": { "type": "string" }
              }
            },
            "SkippedBoolDef": false
          }
        }
        """u8;

    /// <summary>Verifies constructor validation guards on <see cref="JsonSchemaContentLoader"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_InvalidArguments_Throws()
    {
        await Assert.That(static () => new JsonSchemaContentLoader(default(FilePath), RefPrefix)).Throws<ArgumentException>();
        await Assert.That(static () => new JsonSchemaContentLoader(null!, "page", RefPrefix)).Throws<ArgumentNullException>();
        await Assert.That(static () => new JsonSchemaContentLoader("{}"u8.ToArray(), "   ", RefPrefix)).Throws<ArgumentException>();
    }

    /// <summary>Verifies that <see cref="JsonSchemaContentLoader"/> renders Draft 2020-12 JSON schemas with escaped cells, defaults, enums, and <c>$defs</c>.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadAsync_InlineRichSchema_RendersPropertiesAndDefs(CancellationToken cancellationToken)
    {
        var loader = new JsonSchemaContentLoader(RichJsonSchema.ToArray(), "fallback", "reference/schemas");
        var name = Encoding.UTF8.GetString(loader.Name);
        await Assert.That(name).IsEqualTo("json-schema");

        var pages = await loader.LoadAsync(new(default), cancellationToken);
        await Assert.That(pages).HasSingleItem();
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("reference/schemas/app-core-config.md");

        var markdown = Encoding.UTF8.GetString(pages[0].MarkdownBytes);
        await Assert.That(markdown).Contains("title: \"App \\\"Core\\\" \\\\ Config!\"");
        await Assert.That(markdown).Contains("Root configuration object.");
        await Assert.That(markdown).Contains("**Type:** `object`");
        await Assert.That(markdown).Contains("| `host` | `string, null` | **Yes** | `localhost\\|8080 next` | Bind host\\|port line. Allowed: `localhost, 0.0.0.0` |");
        await Assert.That(markdown).Contains("| `port` | `integer` | No | `8080` | Allowed: `80, 8080` |");
        await Assert.That(markdown).Contains("| `tls` | `#/$defs/TlsSettings` | No | `false` |  |");
        await Assert.That(markdown).Contains("| `untyped` | `any` | No | `true` |  |");
        await Assert.That(markdown).Contains("## `TlsSettings`");
    }

    /// <summary>Verifies local file resolution (relative with input root, relative without input root, rooted, and missing) plus YAML normalization.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadAsync_LocalYamlAndRootedFiles_ResolvesAndNormalizes(CancellationToken cancellationToken)
    {
        var dir = Directory.CreateTempSubdirectory("nstd-jsonschema-");
        try
        {
            var yamlPath = Path.Combine(dir.FullName, "service.schema.yaml");
            const string yamlSchema = "definitions:\n  Item:\n    properties:\n      id:\n        type: string\nrequired: notAnArray\nproperties: notAnObject\n";
            await File.WriteAllTextAsync(yamlPath, yamlSchema, cancellationToken);

            var relLoader = new JsonSchemaContentLoader(new FilePath("service.schema.yaml"), default);
            var relPages = await relLoader.LoadAsync(new(new DirectoryPath(dir.FullName)), cancellationToken);
            await Assert.That(relPages[0].RelativePath.Value).IsEqualTo("service-schema.md");
            await Assert.That(Encoding.UTF8.GetString(relPages[0].MarkdownBytes)).Contains("## `Item`");

            var rootedLoader = new JsonSchemaContentLoader(new FilePath(yamlPath), "/nested/");
            var rootedPages = await rootedLoader.LoadAsync(new(default), cancellationToken);
            await Assert.That(rootedPages[0].RelativePath.Value).IsEqualTo("nested/service-schema.md");

            var missingRelativeNoRoot = new JsonSchemaContentLoader(new FilePath("nonexistent-relative-schema.json"), RefPrefix);
            await Assert.That(async () => _ = await missingRelativeNoRoot.LoadAsync(new(default), cancellationToken))
                .Throws<ContentLoaderException>();
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>Verifies helper edge cases (`.md` suffix preservation, symbol-only slugs, array JSON detection, and invalid JSON errors).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HelperAndBuilder_EdgeCases_BehaveDeterministically()
    {
        var targetPath = SchemaSourceHelper.BuildTargetPath("///", "already.md");
        await Assert.That(targetPath.Value).IsEqualTo("already.md");
        await Assert.That(SchemaSourceHelper.Slugify("!!!")).IsEqualTo("schema");
        await Assert.That(Encoding.UTF8.GetString(SchemaSourceHelper.NormalizeJsonOrYaml(" [1]"u8.ToArray()))).IsEqualTo(" [1]");
        await Assert.That(SchemaSourceHelper.NormalizeJsonOrYaml([])).IsNotNull();

        await Assert.That(static () => JsonSchemaPageBuilder.Build([], RefPrefix, "empty")).Throws<ContentLoaderException>();
        await Assert.That(static () => JsonSchemaPageBuilder.Build("[1,2]"u8.ToArray(), RefPrefix, "arr")).Throws<ContentLoaderException>();
        await Assert.That(static () => JsonSchemaPageBuilder.Build("{bad"u8.ToArray(), RefPrefix, "bad")).Throws<ContentLoaderException>();
    }
}
