// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>Bulk-registration helpers wrapping repeated <see cref="DocBuilder.UsePlugin(IPlugin)"/> calls.</summary>
public static class DocBuilderBulkExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers every plugin in <paramref name="plugins"/>, in order.</summary>
        /// <param name="plugins">Plugins to register.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UsePlugins(params ReadOnlySpan<IPlugin> plugins)
        {
            for (var i = 0; i < plugins.Length; i++)
            {
                _ = builder.UsePlugin(plugins[i]);
            }

            return builder;
        }
    }
}
