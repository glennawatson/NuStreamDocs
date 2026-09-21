// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown;

/// <summary>
/// Per-inline-run cache of where the last possible closing run of each emphasis marker and required run length sits.
/// A slot reads zero until it is set.
/// </summary>
internal ref struct EmphasisCloserSlots
{
    /// <summary>Required close run length of one marker.</summary>
    private const int SingleRun = 1;

    /// <summary>Required close run length of two markers.</summary>
    private const int DoubleRun = 2;

    /// <summary>Asterisk runs of one marker.</summary>
    private int _starSingle;

    /// <summary>Asterisk runs of two markers.</summary>
    private int _starDouble;

    /// <summary>Asterisk runs of three or more markers.</summary>
    private int _starTriple;

    /// <summary>Underscore runs of one marker.</summary>
    private int _underscoreSingle;

    /// <summary>Underscore runs of two markers.</summary>
    private int _underscoreDouble;

    /// <summary>Underscore runs of three or more markers.</summary>
    private int _underscoreTriple;

    /// <summary>Gets the slot for a marker and required run length.</summary>
    /// <param name="underscore">True for the underscore marker, false for the asterisk.</param>
    /// <param name="length">Required close run length; three or more share one slot.</param>
    /// <returns>The stored value, or zero when unset.</returns>
    internal readonly int Get(bool underscore, int length) => (underscore, length) switch
    {
        (false, SingleRun) => _starSingle,
        (false, DoubleRun) => _starDouble,
        (false, _) => _starTriple,
        (true, SingleRun) => _underscoreSingle,
        (true, DoubleRun) => _underscoreDouble,
        (true, _) => _underscoreTriple
    };

    /// <summary>Stores the slot for a marker and required run length.</summary>
    /// <param name="underscore">True for the underscore marker, false for the asterisk.</param>
    /// <param name="length">Required close run length; three or more share one slot.</param>
    /// <param name="value">Value to store.</param>
    internal void Set(bool underscore, int length, int value)
    {
        switch (underscore, length)
        {
            case (false, SingleRun):
            {
                _starSingle = value;
                break;
            }

            case (false, DoubleRun):
            {
                _starDouble = value;
                break;
            }

            case (false, _):
            {
                _starTriple = value;
                break;
            }

            case (true, SingleRun):
            {
                _underscoreSingle = value;
                break;
            }

            case (true, DoubleRun):
            {
                _underscoreDouble = value;
                break;
            }

            default:
            {
                _underscoreTriple = value;
                break;
            }
        }
    }
}
