using System;
using System.Numerics;

namespace GlassTL.Telegram.MTProto.Crypto
{
    public class FactorizedPair
    {
        private readonly BigInteger p;
        private readonly BigInteger q;

        public FactorizedPair(BigInteger p, BigInteger q)
        {
            this.p = p;
            this.q = q;
        }

        public FactorizedPair(long p, long q)
        {
            this.p = BigInteger.ValueOf(p);
            this.q = BigInteger.ValueOf(q);
        }

        public BigInteger Min
        {
            get
            {
                return p.Min(q);
            }
        }

        public BigInteger Max
        {
            get
            {
                return p.Max(q);
            }
        }

        //private static long GCD(long a, long b)
        //{
        //    while (a != 0 && b != 0)
        //    {
        //        while ((b & 1) == 0)
        //        {
        //            b >>= 1;
        //        }
        //        while ((a & 1) == 0)
        //        {
        //            a >>= 1;
        //        }
        //        if (a > b)
        //        {
        //            a -= b;
        //        }
        //        else
        //        {
        //            b -= a;
        //        }
        //    }
        //    return b == 0 ? a : b;
        //}
        //public static bool FindIOS(long pq, out FactorizedPair factorized)
        //{
        //    int it = 0, i, j;
        //    long g = 0;

        //    for (i = 0; i < 3 || it < 1000; i++)
        //    {
        //        long t = ((Utils.Helpers.GenerateRandomLong() & 15) + 17) % pq;
        //        long x = Utils.Helpers.GenerateRandomLong() % (pq - 1) + 1, y = x;
        //        int lim = 1 << (i + 18);

        //        for (j = 1; j < lim; j++)
        //        {
        //            ++it;
        //            long a = x, b = x, c = t;
        //            while (b >= 1)
        //            {
        //                if ((b & 1) == 1)
        //                {
        //                    c += a;
        //                    if (c >= pq)
        //                    {
        //                        c -= pq;
        //                    }
        //                }
        //                a += a;
        //                if (a >= pq)
        //                {
        //                    a -= pq;
        //                }
        //                b >>= 1;
        //            }

        //            x = c;
        //            g = GCD(x < y ? pq + x - y : x - y, pq);

        //            if (g != 1)
        //            {
        //                break;
        //            }

        //            if ((j & (j - 1)) == 0)
        //            {
        //                y = x;
        //            }
        //        }
        //        if (g > 1 && g < pq)
        //        {
        //            break;
        //        }
        //    }

        //    if (g > 1 && g < pq)
        //    {
        //        factorized = new FactorizedPair(g, pq / g);
        //        return true;
        //    }
        //    else
        //    {
        //        factorized = null;
        //        return false;
        //    }
        //}

        /// <summary>
        /// Converted from TDesktop.
        /// </summary>
        /// <param name="pq"></param>
        /// <param name="factorized"></param>
        /// <returns></returns>
        public static bool Find(long pq, out FactorizedPair factorized)
        {
            long pqSqrt = (long)Math.Sqrt(pq), ySqr, y;
            
            while (pqSqrt * pqSqrt > pq) pqSqrt--;
            while (pqSqrt * pqSqrt < pq) pqSqrt++;

            while (true)
            {
                ySqr = pqSqrt * pqSqrt - pq;
                y = (long)Math.Sqrt(ySqr);

                while (y * y > ySqr) y--;
                while (y * y < ySqr) y++;

                if (ySqr == 0 || y + pqSqrt >= pq)
                {
                    factorized = null;
                    return false;
                }
                if (y * y == ySqr)
                {
                    factorized = new FactorizedPair(pqSqrt + y, (pqSqrt > y) ? (pqSqrt - y) : (y - pqSqrt));
                    return true;
                }

                pqSqrt++;
            }
        }

        public override string ToString()
        {
            return $"P: {p}, Q: {q}";
        }
    }
}


