using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GA.Api.Events;
using GA.Api.Types;
using GA.Api.Types.Enums;

namespace GA.Api.PhraseEvolution
{
    class Program
    {
        private static readonly Random rnd = new Random();
        private static string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,.";
        private static string phrase = "Those who live in glass houses should not throw stones";

        static void Main(string[] args)
        {
            var g = new GeneticSolver(1024 * 8, 1000, 0.1, 0.1, CrossoverType.UniformCrossover);
            g.GeneratorFunction = PhraseGenerator;
            g.FitnessFunction = CalculateFitness;
            g.IterationCompleted += G_IterationCompleted;
            g.SolutionFound += G_SolutionFound;
            g.Evolve();
        }

        private static void G_SolutionFound(object sender, SolutionFoundEventArgs e)
        {
            Console.WriteLine("Solution found");
            Decode(e.Solution);
        }

        private static void G_IterationCompleted(object sender, IterationCompletedEventArgs e)
        {
            Decode(e.BestChromosome);
        }

        private static void Decode(Chromosome c)
        {
            var s = new StringBuilder();

            foreach (var d in c.Data)
            {
                s.Append(d.ToString());
            }

            Console.WriteLine(s);
        }
      
        public static Chromosome PhraseGenerator()
        {
            var characters = new List<object>();

            for (int i = 0; i < phrase.Length; i++)
            {
                var character_index = rnd.Next(0, letters.Length);
                characters.Add(letters[character_index]);
            }

            return new Chromosome(characters);
        }

        public static double CalculateFitness(Chromosome chromosome)
        {
            return chromosome.Data.Select((gene, index) => System.Math.Abs(phrase[index] - (char)gene)).Sum();
        }      
    }
}
