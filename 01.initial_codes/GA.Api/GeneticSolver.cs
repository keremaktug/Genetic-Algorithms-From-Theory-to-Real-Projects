using System;
using System.Collections.Generic;
using System.Linq;
using GA.Api.Events;
using GA.Api.Operators;
using GA.Api.Types;
using GA.Api.Types.Enums;

namespace GA.Api
{
    public class GeneticSolver
    {
        private static readonly Random rnd = new Random();

        public Population Population { get; set; }

        public int PopulationSize { get; set; }

        public int IterationCount { get; set; }

        public double ElitismRatio { get; set; }

        public double MutationRatio { get; set; }

        public CrossoverType CrossoverType { get; set; }

        public MutationType MutationType { get; set; }

        public SortType SortType { get; set; }

        public delegate double PFitnessFunction(Chromosome chromosome);

        public delegate Chromosome PGeneratorFunction();

        public event EventHandler<IterationCompletedEventArgs> IterationCompleted = delegate { };

        public event EventHandler<SolutionFoundEventArgs> SolutionFound = delegate { };
        
        public PFitnessFunction FitnessFunction { get; set; }
        
        public PGeneratorFunction GeneratorFunction { get; set; }

        public GeneticSolver(int population_size = 1024, int iteration_count = 1000, double elitism_ratio = 0.1, double mutation_ratio = 0.01, CrossoverType crossover_type = CrossoverType.OnePointCrossover)
        {
            PopulationSize = population_size;
            IterationCount = iteration_count;
            ElitismRatio = elitism_ratio;
            MutationRatio = mutation_ratio;
            CrossoverType = crossover_type;
            Population = new Population(population_size);
        }

        private void Init()
        {
            Population.Chromosomes = new List<Chromosome>();

            for (int i = 0; i < PopulationSize; i++)
            {
                Population.Chromosomes.Add(GeneratorFunction());
            }
        }

        private void CalculateFitness()
        {
            foreach (var chromosome in Population.Chromosomes)
            {
                chromosome.Fitness = FitnessFunction(chromosome);
            }
        }

        private void Elitism()
        {
            var new_generation = new List<Chromosome>();

            var elitism_start = (int)(PopulationSize * ElitismRatio);

            for (int i = 0; i < elitism_start; i++)
                new_generation.Add(Population.Chromosomes[i]);

            for (int i = elitism_start; i < PopulationSize; i++)
            {
                int i1 = rnd.Next() % (Population.Chromosomes.Count / 2);
                int i2 = rnd.Next() % (Population.Chromosomes.Count / 2);

                var ch1_data = Population.Chromosomes[i1].Data;
                var ch2_data = Population.Chromosomes[i2].Data;

                List<object> cross_chromosome_data = null;

                int axis = rnd.Next(1, Population.Chromosomes.First().Data.Count);

                switch (CrossoverType)
                {
                    case CrossoverType.OnePointCrossover: 
                        cross_chromosome_data = Crossover.OnePointCrossover(ch1_data, ch2_data, axis).Item1; 
                    break;

                    case CrossoverType.UniformCrossover:                         
                        cross_chromosome_data = Crossover.UniformCrossover(ch1_data, ch2_data).Item1; 
                    break;

                    case CrossoverType.PMX: 
                        cross_chromosome_data = Crossover.PMX(ch1_data, ch2_data); 
                    break;
                }

                var cross_chromosome = new Chromosome(cross_chromosome_data);

                new_generation.Add(cross_chromosome);
            }

            Population.Chromosomes = new_generation;
        }

        private void Mutate()
        {
            int mutation_chromosome = rnd.Next(0, Population.Chromosomes.Count);

            var chromosome = Population.Chromosomes[mutation_chromosome];

            Chromosome mutant = null;

            switch (MutationType)
            {
                case MutationType.Swap: mutant = new Chromosome(Mutation.SwapMutation(chromosome.Data)); break;
                case MutationType.Scramble: mutant = new Chromosome(Mutation.ScrambleMutation(chromosome.Data)); break;
                case MutationType.Inverse: mutant = new Chromosome(Mutation.InversionMutation(chromosome.Data)); break;
            }

            Population.Chromosomes[mutation_chromosome] = mutant;
        }

        public void Evolve()
        {
            Init();

            for(int i = 0; i < IterationCount; i++)
            {
                IterationCompleted(this, new IterationCompletedEventArgs(i, Population.AverageFitness(), Population.GetFittest()));

                Elitism();

                if ((rnd.Next(1000) / 1000) < MutationRatio)
                {
                    Mutate();
                }

                CalculateFitness();
                Population.Sort(SortType);

                if (Population.Chromosomes.First().Fitness == 0)
                {
                    SolutionFound(this, new SolutionFoundEventArgs(i, Population.GetFittest()));
                    break;
                }
            }
        }
    }
}
