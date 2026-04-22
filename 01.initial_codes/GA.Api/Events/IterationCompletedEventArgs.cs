using System;
using GA.Api.Types;

namespace GA.Api.Events
{
    public class IterationCompletedEventArgs : EventArgs
    {
        public int IterationCount { get; }

        public Chromosome BestChromosome { get; }

        public double AverageFitness { get; }

        public IterationCompletedEventArgs(int iteration_count, double average_fitness, Chromosome chromosome)
        {
            IterationCount = iteration_count;
            AverageFitness = average_fitness;
            BestChromosome = chromosome;
        }
    }
}
