namespace AI;

public class LcgRandom
{
    private long seed;
    private readonly long a;
    private readonly long c;
    private readonly long m;

    public LcgRandom(
        long seed,
        long a = 1664525,
        long c = 1013904223,
        long m = 2147483648)
    {
        if (m == 0) throw new ArgumentException("Modulus m must be greater than 0.");
        if (a == 0 || a >= m) throw new ArgumentException("Multiplier a must be in the range 1 <= a < m.");
        if (c >= m) throw new ArgumentException("Increment c must be in the range 0 <= c < m.");

        this.seed = seed;
        this.a = a;
        this.c = c;
        this.m = m;
    }

    public long Next()
    {
        seed = (a * seed + c) % m;
        return seed;
    }

    public int NextInt32()
    {
        return (int)(Next() % int.MaxValue);
    }

    public double NextDouble()
    {
        return (double)Next() / m;
    }

    public byte NextByte()
    {
        return (byte)(Next() & 0xFF);
    }

    public int NextInt32(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentException("minValue must be less than maxValue.");

        return minValue + (NextInt32() % (maxValue - minValue));
    }

    public long Next(long minValue, long maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentException("minValue must be less than maxValue.");

        return minValue + (Next() % (maxValue - minValue));
    }

    public long Next(long minValue, long maxValue, List<long> exclude)
    {
        if (minValue >= maxValue)
            throw new ArgumentException("minValue must be less than maxValue.");

        long result;
        do
        {
            result = Next(minValue, maxValue);
        } while (exclude.Contains(result));

        return result;
    }

    public int NextInt32(int minValue, int maxValue, List<int> exclude)
    {
        if (minValue >= maxValue)
            throw new ArgumentException("minValue must be less than maxValue.");

        int result;
        do
        {
            result = NextInt32(minValue, maxValue);
        } while (exclude.Contains(result));

        return result;
    }

    internal int? NextInt32(object minConnectivityDelayTicks, object maxConnectivityDelayTicks)
    {
        throw new NotImplementedException();
    }

    internal LcgRandom Clone()
    {
        return new LcgRandom(seed, a, c, m);
    }
}
