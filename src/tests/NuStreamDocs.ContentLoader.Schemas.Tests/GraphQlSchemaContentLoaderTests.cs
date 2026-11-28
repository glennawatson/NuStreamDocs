// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.Schemas.Tests;

/// <summary>Tests for <see cref="GraphQlSchemaContentLoader"/> and <see cref="GraphQlSdlPageBuilder"/>.</summary>
public sealed class GraphQlSchemaContentLoaderTests
{
    /// <summary>Loader name and route segment for GraphQL tests.</summary>
    private const string GraphQlKey = "graphql";

    /// <summary>API route prefix used in builder test cases.</summary>
    private const string ApiPrefix = "api";

    /// <summary>Gets sample SDL covering block/single descriptions, types, inputs, interfaces, enums, directives, comments, and scalars.</summary>
    private static ReadOnlySpan<byte> SampleSdl => """
        # Leading comment
        scalar DateTime
        schema {
          query: Query
        }
        @rootDirective

        "Root query operations"
        type Query {
          "Fetch a user by identifier"
          user(id: ID!, filter: String = "a:b"): User @auth
          orphanField
          strayClose): Int
        }

        input UpdateUserInput {
          displayName: String!
        }

        interface Node {
          id: ID!
        }

        enum Role {
          "Administrator role"
          ADMIN @deprecated
          MEMBER
        }

        type EmptyType {
        }
        """u8;

    /// <summary>Verifies constructor guards on <see cref="GraphQlSchemaContentLoader"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Constructor_InvalidArguments_Throws()
    {
        await Assert.That(static () => new GraphQlSchemaContentLoader(default(FilePath), GraphQlKey)).Throws<ArgumentException>();
        await Assert.That(static () => new GraphQlSchemaContentLoader((byte[])null!, "Title", GraphQlKey)).Throws<ArgumentNullException>();
        await Assert.That(static () => new GraphQlSchemaContentLoader("type Q { id: ID }"u8.ToArray(), string.Empty, GraphQlKey)).Throws<ArgumentException>();
    }

    /// <summary>Verifies SDL parsing for types, inputs, interfaces, enums, arguments, directives, and block descriptions.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadAsync_InlineAndFileSdl_RendersAllTypeKinds(CancellationToken cancellationToken)
    {
        var inlineLoader = new GraphQlSchemaContentLoader(SampleSdl.ToArray(), "GraphQL API", "reference/graphql");
        var loaderName = Encoding.UTF8.GetString(inlineLoader.Name);
        await Assert.That(loaderName).IsEqualTo(GraphQlKey);

        var pages = await inlineLoader.LoadAsync(new(default), cancellationToken);
        await Assert.That(pages).HasSingleItem();
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("reference/graphql/graphql-api.md");

        var markdown = Encoding.UTF8.GetString(pages[0].MarkdownBytes);
        await Assert.That(markdown).Contains("## `Query` (type)");
        await Assert.That(markdown).Contains("Root query operations");
        await Assert.That(markdown).Contains("| `user(id: ID!, filter: String = \"a:b\")` | `User` | Fetch a user by identifier |");
        await Assert.That(markdown).Contains("| `orphanField` | `` |  |");
        await Assert.That(markdown).Contains("## `UpdateUserInput` (input)");
        await Assert.That(markdown).Contains("## `Node` (interface)");
        await Assert.That(markdown).Contains("## `Role` (enum)");
        await Assert.That(markdown).Contains("| `ADMIN` | Administrator role |");
        await Assert.That(markdown).Contains("| `MEMBER` |  |");
        await Assert.That(markdown).Contains("## `EmptyType` (type)");

        var dir = Directory.CreateTempSubdirectory("nstd-gql-");
        try
        {
            var file = Path.Combine(dir.FullName, "schema.graphql");
            await File.WriteAllTextAsync(file, "\"\"\"Block desc\"\"\"\ntype Ping { ok: Boolean! }", cancellationToken);

            var defaultTitleLoader = new GraphQlSchemaContentLoader(new FilePath("schema.graphql"), ApiPrefix);
            var filePages = await defaultTitleLoader.LoadAsync(new(new DirectoryPath(dir.FullName)), cancellationToken);
            await Assert.That(filePages[0].RelativePath.Value).IsEqualTo("api/schema.md");
            await Assert.That(Encoding.UTF8.GetString(filePages[0].MarkdownBytes)).Contains("Block desc");

            var customTitleLoader = new GraphQlSchemaContentLoader(new FilePath(file), ApiPrefix, "Custom GraphQL");
            var customPages = await customTitleLoader.LoadAsync(new(default), cancellationToken);
            await Assert.That(customPages[0].RelativePath.Value).IsEqualTo("api/custom-graphql.md");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>Verifies empty and truncated SDL edge cases return gracefully without throwing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Build_EmptyAndTruncatedConstructs_HandlesCleanly()
    {
        await Assert.That(GraphQlSdlPageBuilder.Build("# trailing comment without newline"u8, "CommentOnly", ApiPrefix)).IsEmpty();
        await Assert.That(GraphQlSdlPageBuilder.Build("scalar OnlyScalar   "u8, "Empty", ApiPrefix)).IsEmpty();
        await Assert.That(GraphQlSdlPageBuilder.Build("schema { query: Query"u8, "UnclosedSchema", ApiPrefix)).IsEmpty();
        await Assert.That(GraphQlSdlPageBuilder.Build("\"\"\"unclosed block"u8, "UnclosedBlock", ApiPrefix)).IsEmpty();
        await Assert.That(GraphQlSdlPageBuilder.Build("\"unclosed single"u8, "UnclosedSingle", ApiPrefix)).IsEmpty();

        var unclosedBrace = GraphQlSdlPageBuilder.Build("type BrokenNoBrace"u8, "Broken", ApiPrefix);
        await Assert.That(unclosedBrace).HasSingleItem();

        var unclosedMembers = GraphQlSdlPageBuilder.Build("type Open { id: ID!\n   "u8, "Open", ApiPrefix);
        await Assert.That(Encoding.UTF8.GetString(unclosedMembers[0].MarkdownBytes)).Contains("| `id` | `ID!` |");
    }
}
