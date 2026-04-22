using System.Collections.Generic;

namespace GA.Api.Types
{
    public class Chromosome
    {
        public List<object> Data { get; set; }

        public double Fitness { get; set; }

        public Chromosome()
        {
            Data = new List<object>();
        }

        public Chromosome(List<object> data)
        {
            Data = data;
        }

        public override string ToString()
        {
            return $"Data : {string.Join(",", Data)} Fitness : {Fitness}";
        }
    }
}
