// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Reference-link definition table. Labels match case-insensitively (ASCII) with internal
/// whitespace runs collapsed, and the first definition of a label wins. Entries hold offsets into
/// the source the definitions were read from, so the table never copies label, href or title bytes.
/// </summary>
internal sealed class LinkReferenceTable
{
    /// <summary>Smallest bucket count, so a tiny table still probes with a valid mask.</summary>
    private const int MinBucketCount = 8;

    /// <summary>FNV-1a offset basis.</summary>
    private const uint HashSeed = 2_166_136_261;

    /// <summary>FNV-1a prime.</summary>
    private const uint HashPrime = 16_777_619;

    /// <summary>Factor applied to the entry array when it fills.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Bucket heads; each value is an entry index plus one, or zero for an empty bucket.</summary>
    private int[] _buckets = new int[MinBucketCount];

    /// <summary>Definition entries in insertion order.</summary>
    private Entry[] _entries = new Entry[MinBucketCount];

    /// <summary>Bucket count in use for the current fill; always a power of two.</summary>
    private int _bucketCount = MinBucketCount;

    /// <summary>Gets the number of definitions stored.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the number of definitions the table can hold without growing.</summary>
    internal int Capacity => _entries.Length;

    /// <summary>Empties the table and makes room for <paramref name="expected"/> definitions.</summary>
    /// <param name="expected">Upper bound on the definitions about to be added.</param>
    internal void Reset(int expected)
    {
        var bucketCount = MinBucketCount;
        while (bucketCount < expected)
        {
            bucketCount <<= 1;
        }

        if (_buckets.Length < bucketCount)
        {
            _buckets = new int[bucketCount];
        }
        else
        {
            Array.Clear(_buckets, 0, bucketCount);
        }

        if (_entries.Length < expected)
        {
            _entries = new Entry[bucketCount];
        }

        _bucketCount = bucketCount;
        Count = 0;
    }

    /// <summary>Adds a definition unless one with an equivalent label is already present.</summary>
    /// <param name="source">UTF-8 source the ranges index into.</param>
    /// <param name="label">Label range.</param>
    /// <param name="href">Href range.</param>
    /// <param name="title">Title range; empty when the definition has no title.</param>
    /// <returns>True when the definition was added; false when the label was already defined.</returns>
    internal bool TryAdd(ReadOnlySpan<byte> source, in ByteRange label, in ByteRange href, in ByteRange title)
    {
        var labelBytes = label.AsSpan(source);
        var hash = HashLabel(labelBytes);
        var bucket = hash & (_bucketCount - 1);
        for (var i = _buckets[bucket] - 1; i >= 0; i = _entries[i].Next)
        {
            ref readonly var existing = ref _entries[i];
            if (existing.Hash == hash && LabelsMatch(existing.Label.AsSpan(source), labelBytes))
            {
                return false;
            }
        }

        if (Count == _entries.Length)
        {
            Array.Resize(ref _entries, Count * GrowthFactor);
        }

        _entries[Count] = new(hash, _buckets[bucket] - 1, label, href, title);
        Count++;
        _buckets[bucket] = Count;
        return true;
    }

    /// <summary>Looks up the definition whose label is equivalent to <paramref name="label"/>.</summary>
    /// <param name="source">UTF-8 source the stored ranges index into.</param>
    /// <param name="label">Label bytes, taken from any span.</param>
    /// <param name="href">Href range of the definition on success.</param>
    /// <param name="title">Title range of the definition on success.</param>
    /// <returns>True when the label is defined.</returns>
    internal bool TryGet(ReadOnlySpan<byte> source, ReadOnlySpan<byte> label, out ByteRange href, out ByteRange title)
    {
        var hash = HashLabel(label);
        var bucket = hash & (_bucketCount - 1);
        for (var i = _buckets[bucket] - 1; i >= 0; i = _entries[i].Next)
        {
            ref readonly var existing = ref _entries[i];
            if (existing.Hash != hash || !LabelsMatch(existing.Label.AsSpan(source), label))
            {
                continue;
            }

            href = existing.Href;
            title = existing.Title;
            return true;
        }

        href = default;
        title = default;
        return false;
    }

    /// <summary>Hashes a label after case folding and whitespace collapsing.</summary>
    /// <param name="label">Label bytes.</param>
    /// <returns>Non-negative hash.</returns>
    private static int HashLabel(ReadOnlySpan<byte> label)
    {
        var hash = HashSeed;
        var reader = new NormalizedLabelReader(label);
        while (reader.TryRead(out var b))
        {
            hash = (hash ^ b) * HashPrime;
        }

        return (int)(hash & int.MaxValue);
    }

    /// <summary>Compares two labels after case folding and whitespace collapsing.</summary>
    /// <param name="left">First label.</param>
    /// <param name="right">Second label.</param>
    /// <returns>True when the labels are equivalent.</returns>
    private static bool LabelsMatch(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var leftReader = new NormalizedLabelReader(left);
        var rightReader = new NormalizedLabelReader(right);
        while (true)
        {
            var hasLeft = leftReader.TryRead(out var l);
            var hasRight = rightReader.TryRead(out var r);
            if (hasLeft != hasRight || l != r)
            {
                return false;
            }

            if (!hasLeft)
            {
                return true;
            }
        }
    }

    /// <summary>One stored definition.</summary>
    /// <param name="Hash">Normalized label hash.</param>
    /// <param name="Next">Index of the next entry in the same bucket, or <c>-1</c>.</param>
    /// <param name="Label">Label range.</param>
    /// <param name="Href">Href range.</param>
    /// <param name="Title">Title range.</param>
    private readonly record struct Entry(int Hash, int Next, ByteRange Label, ByteRange Href, ByteRange Title);

    /// <summary>Streams a label's bytes with ASCII letters lowercased, whitespace runs collapsed to one space, and outer whitespace dropped.</summary>
    private ref struct NormalizedLabelReader
    {
        /// <summary>Label being read.</summary>
        private readonly ReadOnlySpan<byte> _label;

        /// <summary>Next unread index.</summary>
        private int _position;

        /// <summary>True once a non-whitespace byte has been emitted.</summary>
        private bool _emitted;

        /// <summary>Initializes a new instance of the <see cref="NormalizedLabelReader"/> struct.</summary>
        /// <param name="label">Label bytes.</param>
        public NormalizedLabelReader(ReadOnlySpan<byte> label)
        {
            _label = label;
            _position = 0;
            _emitted = false;
        }

        /// <summary>Reads the next normalized byte.</summary>
        /// <param name="value">The byte on success.</param>
        /// <returns>True when a byte was produced; false at the end of the label.</returns>
        public bool TryRead(out byte value)
        {
            while (_position < _label.Length)
            {
                var b = _label[_position];
                if (!AsciiByteHelpers.IsAsciiWhitespace(b))
                {
                    _position++;
                    _emitted = true;
                    value = AsciiByteHelpers.ToAsciiLowerByte(b);
                    return true;
                }

                _position = AsciiByteHelpers.SkipWhitespace(_label, _position);
                if (!_emitted || _position >= _label.Length)
                {
                    continue;
                }

                value = (byte)' ';
                return true;
            }

            value = 0;
            return false;
        }
    }
}
