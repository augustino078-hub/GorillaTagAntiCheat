using System;
using GorillaAntiCheat.Core.Buffers;
using Xunit;

namespace GorillaAntiCheat.Tests
{
    public class RingBufferTests
    {
        [Fact]
        public void Add_TracksCountUpToCapacity()
        {
            var buf = new RingBuffer<int>(3);
            Assert.True(buf.IsEmpty);
            buf.Add(1);
            buf.Add(2);
            Assert.Equal(2, buf.Count);
            Assert.False(buf.IsFull);
            buf.Add(3);
            Assert.True(buf.IsFull);
            Assert.Equal(3, buf.Count);
        }

        [Fact]
        public void Add_OverwritesOldestWhenFull()
        {
            var buf = new RingBuffer<int>(3);
            buf.Add(1);
            buf.Add(2);
            buf.Add(3);
            buf.Add(4); // overwrites 1

            Assert.Equal(3, buf.Count);
            Assert.Equal(2, buf[0]);   // oldest
            Assert.Equal(4, buf[2]);   // newest
            Assert.Equal(4, buf.FromEnd(0));
            Assert.Equal(2, buf.FromEnd(2));
        }

        [Fact]
        public void TryGetNewestAndOldest_Work()
        {
            var buf = new RingBuffer<int>(2);
            Assert.False(buf.TryGetNewest(out _));
            buf.Add(10);
            buf.Add(20);
            buf.Add(30); // overwrite 10
            Assert.True(buf.TryGetNewest(out int newest));
            Assert.True(buf.TryGetOldest(out int oldest));
            Assert.Equal(30, newest);
            Assert.Equal(20, oldest);
        }

        [Fact]
        public void IndexOutOfRange_Throws()
        {
            var buf = new RingBuffer<int>(2);
            buf.Add(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => buf[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => buf.FromEnd(1));
        }

        [Fact]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
        }
    }
}
