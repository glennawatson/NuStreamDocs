// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Builder-extension + options tests for <c>LinkValidatorPlugin</c>.</summary>
public class LinkValidatorRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new LinkValidatorPlugin().Name).IsEqualTo("link-validator");

    /// <summary>Default LinkValidatorOptions has expected defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultLinkValidatorOptions()
    {
        var defaults = LinkValidatorOptions.Default;
        await Assert.That(defaults.StrictInternal).IsFalse();
        await Assert.That(defaults.StrictExternal).IsFalse();
        await Assert.That(defaults.Parallelism).IsGreaterThan(0);
    }

    /// <summary>Default ExternalLinkValidatorOptions has expected defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultExternalOptions()
    {
        var defaults = ExternalLinkValidatorOptions.Default;
        await Assert.That(defaults.UserAgent).IsNotEqualTo(string.Empty);
        await Assert.That(defaults.MaxRequestsPerHost).IsGreaterThan(0);
        await Assert.That(defaults.MaxRetries).IsGreaterThanOrEqualTo(0);
    }

    /// <summary>LinkValidatorOptions.Validate() throws on non-positive Parallelism.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnZeroParallelism()
    {
        var bad = LinkValidatorOptions.Default with { Parallelism = 0 };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(bad.Validate);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>ExternalLinkValidatorOptions.Validate() throws on each invalid field.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExternalValidateThrowsOnInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () =>
            (ExternalLinkValidatorOptions.Default with { MaxRequestsPerHost = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(static () =>
            (ExternalLinkValidatorOptions.Default with { WindowSeconds = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(static () =>
            (ExternalLinkValidatorOptions.Default with { MaxConcurrencyPerHost = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(static () =>
            (ExternalLinkValidatorOptions.Default with { MaxRetries = -1 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(static () =>
            (ExternalLinkValidatorOptions.Default with { RequestTimeoutSeconds = 0 }).Validate());
        var ex = Assert.Throws<ArgumentException>(static () =>
            (ExternalLinkValidatorOptions.Default with { UserAgent = string.Empty }).Validate());
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseLinkValidator() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLinkValidatorRegisters() =>
        await Assert.That(new DocBuilder().UseLinkValidator()).IsTypeOf<DocBuilder>();

    /// <summary>UseLinkValidator(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLinkValidatorOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseLinkValidator(LinkValidatorOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseLinkValidator(options, logger) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLinkValidatorLoggerRegisters() =>
        await Assert.That(new DocBuilder().UseLinkValidator(LinkValidatorOptions.Default, NullLogger.Instance)).IsTypeOf<DocBuilder>();

    /// <summary>UseLinkValidator rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLinkValidatorRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderLinkValidatorExtensions.UseLinkValidator(null!));
        await Assert.That(ex).IsNotNull();
    }
}
