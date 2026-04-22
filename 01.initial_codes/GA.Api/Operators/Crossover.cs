using System;
using System.Collections.Generic;
using System.Linq;

namespace GA.Api.Operators
{
    public static class Crossover
    {
        private static readonly Random rnd = new Random();

        public static Tuple<List<object>, List<object>> OnePointCrossover(List<object> a, List<object> b, int point)
        {
            if (a.Count != b.Count) throw new Exception("Chromosome lengths are not equal !");

            if (point > a.Count) throw new Exception("Point are not true");

            var child1 = new List<object>();
            var child2 = new List<object>();

            for (int i = 0; i < point; i++)
            {
                child1.Add(a[i]);
            }

            for (int i = point; i < b.Count; i++)
            {
                child1.Add(b[i]);
            }

            for (int i = 0; i < point; i++)
            {
                child2.Add(b[i]);
            }

            for (int i = point; i < b.Count; i++)
            {
                child2.Add(a[i]);
            }

            return new Tuple<List<object>, List<object>>(child1, child2);
        }

        public static Tuple<List<object>, List<object>> UniformCrossover(List<object> a, List<object> b)
        {
            if (a.Count != b.Count) throw new Exception("Chromosome lengths are not equal !");

            var child1 = new List<object>();
            var child2 = new List<object>();

            for (int i = 0; i < a.Count; i++)
            {
                var coin = rnd.Next(0, 2);

                if (coin == 0)
                {
                    child1.Add(a[i]);
                    child2.Add(b[i]);
                }
                else
                {
                    child1.Add(b[i]);
                    child2.Add(a[i]);
                }
            }

            return new Tuple<List<object>, List<object>>(child1, child2);
        }

        public static List<object> PMX(List<object> a, List<object> b)
        {
            if (a.Count != b.Count) throw new Exception("Chromosome lengths are not equal !");

            var indices = new List<int>();

            for (int i = 0; i < a.Count; i++) indices.Add(i);

            var positions = indices.OrderBy(x => rnd.Next()).Take(2).OrderBy(x => x).ToList();

            int pos = positions.First();
            int size = positions.Last() - pos;

            int n = a.Count;

            if ((pos + size) > n) throw new Exception("PMX pos and size are not true");

            var r = new List<object>();

            int swath_beg = pos;
            int swath_end = pos + size;

            // Setup Child

            r.Fill(null, n);

            // Copy Swath

            for (int i = pos; i < pos + size; i++) r[i] = a[i];

            // Compare 2nd parent and swath

            var other_part = new List<object>();

            for (int i = swath_beg; i < swath_end; i++)
            {
                if (!r.Contains(b[i]))
                {
                    other_part.Add(b[i]);
                }
            }

            // Copy missing genes

            int other_index = 0;

            for (int i = 0; i < r.Count; i++)
            {
                if (r[i] == null)
                {
                    if (other_index < other_part.Count)
                        r[i] = other_part[other_index];
                }

                other_index++;
            }

            var remaining = a.Except(r).ToList();

            other_index = 0;

            for (int i = 0; i < r.Count; i++)
            {
                if (r[i] == null)
                {
                    r[i] = remaining[other_index];
                    other_index++;
                }
            }

            return r;
        }
    }
}
