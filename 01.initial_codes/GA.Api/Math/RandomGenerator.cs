using System;
using System.Collections.Generic;

namespace GA.Api.Math
{
    public static class RandomGenerator
    {
        private static readonly Random Random = new Random(32767);

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                int k = (Random.Next(0, n) % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }        
    }
}
