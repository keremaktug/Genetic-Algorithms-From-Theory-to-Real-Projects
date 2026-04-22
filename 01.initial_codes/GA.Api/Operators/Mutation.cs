using System;
using System.Collections.Generic;
using System.Linq;
using GA.Api.Math;

namespace GA.Api.Operators
{
    public static class Mutation
    {
        private static readonly Random rnd = new Random();

        public static List<object> SwapMutation(List<object> a)
        {
            var r = new List<object>();

            var indices = new List<int>();

            for (int i = 0; i < a.Count; i++) indices.Add(i);

            var swap_indices = indices.OrderBy(x => rnd.Next()).Take(2).ToList().OrderBy(x => x);

            var swap_i1 = (int)swap_indices.First();
            var swap_i2 = (int)swap_indices.Last();

            var swap_val1 = a[swap_i1];
            var swap_val2 = a[swap_i2];

            for (int i = 0; i < a.Count; i++)
            {
                if (i == swap_i1)
                    r.Add(swap_val2);
                else if (i == swap_i2)
                    r.Add(swap_val1);
                else
                    r.Add(a[i]);
            }

            return r;
        }

        public static List<object> ScrambleMutation(List<object> a)
        {
            var r = new List<object>();

            var indices = new List<int>();

            for (int i = 0; i < a.Count; i++) indices.Add(i);

            var scramble_indices = indices.OrderBy(x => rnd.Next()).Take(2).ToList().OrderBy(x => x);

            var scramble_i1 = (int)scramble_indices.First();
            var scramble_i2 = (int)scramble_indices.Last();

            var scramble_part = new List<object>();

            for (int i = scramble_i1; i < scramble_i2; i++)
            {
                scramble_part.Add(a[i]);
            }

            RandomGenerator.Shuffle(scramble_part);

            for (int i = 0; i < scramble_i1; i++) r.Add(a[i]);

            for (int i = 0; i < scramble_part.Count; i++) r.Add(scramble_part[i]);

            for (int i = scramble_i2; i < a.Count; i++) r.Add(a[i]);

            return r;
        }

        public static List<object> InversionMutation(List<object> a)
        {
            var r = new List<object>();

            var indices = new List<int>();

            for (int i = 0; i < a.Count; i++) indices.Add(i);

            var scramble_indices = indices.OrderBy(x => rnd.Next()).Take(2).ToList().OrderBy(x => x);

            var scramble_i1 = (int)scramble_indices.First();
            var scramble_i2 = (int)scramble_indices.Last();

            var scramble_part = new List<object>();

            for (int i = scramble_i1; i < scramble_i2; i++)
            {
                scramble_part.Add(a[i]);
            }

            scramble_part.Reverse();

            for (int i = 0; i < scramble_i1; i++) r.Add(a[i]);

            for (int i = 0; i < scramble_part.Count; i++) r.Add(scramble_part[i]);

            for (int i = scramble_i2; i < a.Count; i++) r.Add(a[i]);

            return r;
        }
    }
}
