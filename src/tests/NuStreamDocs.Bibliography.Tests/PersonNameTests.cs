// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Tests for the <see cref="PersonName"/> record.</summary>
public class PersonNameTests
{
    /// <summary>Institutional names set the literal field.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Institutional_creates_literal_name()
    {
        var name = PersonName.Institutional("The High Court");
        await Assert.That(name.Literal.AsSpan().SequenceEqual("The High Court"u8)).IsTrue();
        await Assert.That(name.IsInstitutional).IsTrue();
        await Assert.That(name.Given.Length).IsEqualTo(0);
        await Assert.That(name.Family.Length).IsEqualTo(0);
    }
}
