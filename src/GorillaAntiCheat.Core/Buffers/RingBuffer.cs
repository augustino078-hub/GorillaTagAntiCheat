using System;

namespace GorillaAntiCheat.Core.Buffers
{
    /// <summary>
    /// Fixed-capacity circular buffer backed by a single pre-allocated array.
    /// After construction it performs no heap allocations, which makes it safe
    /// to use inside the per-tick hot loop on a VR target.
    /// Index 0 is always the oldest retained element; <see cref="Count"/>-1 the newest.
    /// </summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] _items;
        private int _start; // index of the oldest element
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Ring buffer capacity must be positive.");
            _items = new T[capacity];
        }

        public int Capacity => _items.Length;

        public int Count => _count;

        public bool IsFull => _count == _items.Length;

        public bool IsEmpty => _count == 0;

        /// <summary>Appends an item, overwriting the oldest element once full.</summary>
        public void Add(in T item)
        {
            int writeIndex = (_start + _count) % _items.Length;
            if (_count == _items.Length)
            {
                // Full: overwrite oldest and advance the window.
                _items[writeIndex] = item;
                _start = (_start + 1) % _items.Length;
            }
            else
            {
                _items[writeIndex] = item;
                _count++;
            }
        }

        /// <summary>Indexed from the oldest (0) to the newest (Count-1).</summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _items[(_start + index) % _items.Length];
            }
        }

        /// <summary>Returns the element <paramref name="offset"/> ticks back from the newest (0 == newest).</summary>
        public T FromEnd(int offset)
        {
            if ((uint)offset >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(offset));
            int logical = _count - 1 - offset;
            return _items[(_start + logical) % _items.Length];
        }

        public bool TryGetNewest(out T value)
        {
            if (_count == 0)
            {
                value = default!;
                return false;
            }
            value = _items[(_start + _count - 1) % _items.Length];
            return true;
        }

        public bool TryGetOldest(out T value)
        {
            if (_count == 0)
            {
                value = default!;
                return false;
            }
            value = _items[_start];
            return true;
        }

        public void Clear()
        {
            // Release references for GC-tracked T; cheap for value types.
            Array.Clear(_items, 0, _items.Length);
            _start = 0;
            _count = 0;
        }
    }
}
