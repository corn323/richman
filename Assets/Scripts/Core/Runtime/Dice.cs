using System;

namespace Richman.Core
{
    public sealed class DiceResult
    {
        public DiceResult(int first, int second)
        {
            if (first < 1 || first > 6) throw new ArgumentOutOfRangeException(nameof(first));
            if (second < 1 || second > 6) throw new ArgumentOutOfRangeException(nameof(second));
            First = first;
            Second = second;
        }

        public int First { get; }
        public int Second { get; }
        public int Total => First + Second;
    }

    public interface IDiceRoller
    {
        DiceResult Roll();
    }

    public interface IGameRandom
    {
        int NextInt(int minInclusive, int maxExclusive);
    }

    public sealed class SeededRandom : IGameRandom
    {
        private uint _state;

        public SeededRandom(int seed)
        {
            _state = unchecked((uint)seed);
            if (_state == 0) _state = 0x6D2B79F5u;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive) throw new ArgumentException("The random range is invalid.");
            var value = NextUInt();
            return minInclusive + (int)(value % (uint)(maxExclusive - minInclusive));
        }

        private uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }

    public sealed class SeededDiceRoller : IDiceRoller
    {
        private readonly IGameRandom _random;

        public SeededDiceRoller(int seed) : this(new SeededRandom(seed)) { }

        public SeededDiceRoller(IGameRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public DiceResult Roll()
        {
            return new DiceResult(_random.NextInt(1, 7), _random.NextInt(1, 7));
        }
    }
}
