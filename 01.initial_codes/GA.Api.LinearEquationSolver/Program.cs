using System;
using System.Collections.Generic;
using GA.Api.Events;
using GA.Api.Types;

namespace GA.Api.LinearEquationSolver
{
    class Program
    {
        private static readonly Random rnd = new Random();
        private static readonly int Target = 100;

        static void Main(string[] args)
        {
            var g = new GeneticSolver(10, 100, 0.25, 0.01, Types.Enums.CrossoverType.UniformCrossover);
            g.GeneratorFunction = VariablesGenerator;
            g.FitnessFunction = CalculateFitness;
            g.IterationCompleted += G_IterationCompleted;
            g.SolutionFound += G_SolutionFound;
            g.Evolve();
        }

        public static Chromosome VariablesGenerator()
        {
            var variables = new List<object>();

            for (int i = 0; i < 4; i++)
            {
                variables.Add(rnd.Next(0, 100));
            }

            return new Chromosome(variables);
        }

        public static double CalculateFitness(Chromosome chromosome)
        {
            var v1 = (int)chromosome.Data[0];
            var v2 = (int)chromosome.Data[1];
            var v3 = (int)chromosome.Data[2];
            var v4 = (int)chromosome.Data[3];
            return System.Math.Abs(Target - (v1 + v2 + v3 + v4));
        }

        private static void G_IterationCompleted(object sender, IterationCompletedEventArgs e)
        {
            var chromosome = e.BestChromosome;
            var v1 = (int)chromosome.Data[0];
            var v2 = (int)chromosome.Data[1];
            var v3 = (int)chromosome.Data[2];
            var v4 = (int)chromosome.Data[3];
            Console.WriteLine($"Iteration : {e.IterationCount} Average Fitness : {e.AverageFitness} | {v1} + {v2} + {v3} + {v4} = {v1 + v2 + v3 + v4}");
        }

        private static void G_SolutionFound(object sender, SolutionFoundEventArgs e)
        {
            var chromosome = e.Solution;
            var v1 = (int)chromosome.Data[0];
            var v2 = (int)chromosome.Data[1];
            var v3 = (int)chromosome.Data[2];
            var v4 = (int)chromosome.Data[3];
            Console.WriteLine($"{v1} {v2} {v3} {v4} | {v1 + v2 + v3 + v4}");
        }
    }
}
