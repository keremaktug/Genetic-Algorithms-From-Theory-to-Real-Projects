using System;
using System.Collections.Generic;
using System.Linq;
using GA.Api.Types.Enums;

namespace GA.Api.Types
{
    public class Population
    {
        private static readonly Random rnd = new Random();

        public int Size { get; set; }

        public List<Chromosome> Chromosomes;

        public Population(int size)
        {
            Size = size;
            Chromosomes = new List<Chromosome>();
        }
        
        public void Sort(SortType sort_type)
        {
            switch(sort_type)
            {
                case SortType.Ascending:
                    Chromosomes = Chromosomes.OrderBy(x => x.Fitness).ToList();
                break;

                case SortType.Descending:
                    Chromosomes = Chromosomes.OrderByDescending(x => x.Fitness).ToList();
                break;
            }
        }

        public Chromosome GetFittest()
        {
            return Chromosomes.First();
        }

        public double AverageFitness()
        {
            return Chromosomes.Average(x => x.Fitness);
        }
    }
}
