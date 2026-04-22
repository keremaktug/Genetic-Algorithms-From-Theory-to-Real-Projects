using System;
using System.Collections.Generic;
using System.IO;

namespace GA.Api.IO
{
    public class TSPReader
    {
        public Tuple<List<int>, List<double>, List<double>> Read(string filename)
        {
            var ids = new List<int>();
            var x = new List<double>();
            var y = new List<double>();
            
            using (var sr = new StreamReader(filename))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    string[] parts = line.Split(' ');
                    ids.Add(Int32.Parse(parts[0].Trim()));
                    x.Add(Double.Parse(parts[1].Trim()));
                    y.Add(Double.Parse(parts[2].Trim()));
                }
            }

            return new Tuple<List<int>, List<double>, List<double>>(ids, x, y);
        }
    }
}
